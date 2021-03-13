using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.SaveEditor
{
  internal class MultiColumnTreeView : TreeViewWithTreeModel<SaveEditorTreeElement>
  {
    const float kRowHeights = 20f;
    const float kToggleWidth = 18f;
    public bool showControls = true;
    SaveEditorTreeGenerator treeGenerator;

    // All columns
    enum MyColumns
    {
      Name,
      Value,
      Controls,
    }

    public static void TreeToList (TreeViewItem root, IList<TreeViewItem> result)
    {
      if (root == null)
        throw new NullReferenceException("root");
      if (result == null)
        throw new NullReferenceException("result");

      result.Clear();

      if (root.children == null)
        return;

      Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
      for (int i = root.children.Count - 1; i >= 0; i--)
        stack.Push(root.children[i]);

      while (stack.Count > 0)
      {
        TreeViewItem current = stack.Pop();
        result.Add(current);

        if (current.hasChildren && current.children[0] != null)
        {
          for (int i = current.children.Count - 1; i >= 0; i--)
          {
            stack.Push(current.children[i]);
          }
        }
      }
    }

    public MultiColumnTreeView (
      TreeViewState state,
      MultiColumnHeader multicolumnHeader,
      TreeModel<SaveEditorTreeElement> model,
      SaveEditorTreeGenerator treeGenerator
    ) : base (state, multicolumnHeader, model)
    {
      // Custom setup
      rowHeight = kRowHeights;
      columnIndexForTreeFoldouts = 0;
      showAlternatingRowBackgrounds = true;
      showBorder = true;
      customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
      extraSpaceBeforeIconAndLabel = kToggleWidth;
      this.treeGenerator = treeGenerator;

      Reload();
    }


    // Note we We only build the visible rows, only the backend has the full tree information.
    // The treeview only creates info for the row list.
    protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
    {
      var rows = base.BuildRows (root);
      return rows;
    }


    protected override void RowGUI (RowGUIArgs args)
    {
      var item = (TreeViewItem<SaveEditorTreeElement>) args.item;

      for (int i = 0; i < args.GetNumVisibleColumns (); ++i)
      {
        CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
      }
    }

    static Type[] numberTypes = new Type[]{ typeof(int), typeof(uint), typeof(float), typeof(double), typeof(ushort), typeof(short), typeof(UInt64), typeof(Int64)};

    GUIStyle _defaultGuiStyle;
    GUIStyle defaultGuiStyle {
      get{
        if(_defaultGuiStyle == null){
          _defaultGuiStyle = new GUIStyle(GUI.skin.textField);
        }
        return _defaultGuiStyle;
      }
    }
    GUIStyle _errorGuiStyle;
    GUIStyle errorGuiStyle {
      get{
        if(_errorGuiStyle == null){
          _errorGuiStyle = new GUIStyle(defaultGuiStyle);
          _errorGuiStyle.normal.textColor = Color.red;
        }
        return _errorGuiStyle;
      }
    }
    GUIStyle _changedGuiStyle;
    GUIStyle changedGuiStyle {
      get{
        if(_changedGuiStyle == null){
          _changedGuiStyle = new GUIStyle(defaultGuiStyle);
          _changedGuiStyle.fontStyle = FontStyle.Bold;
        }
        return _changedGuiStyle;
      }
    }

    GUIStyle _defaultDropdownStyle;
    GUIStyle defaultDropdownStyle {
      get{
        if(_defaultDropdownStyle == null){
          _defaultDropdownStyle = new GUIStyle(EditorStyles.popup);
        }
        return _defaultDropdownStyle;
      }
    }

    GUIStyle _changeDropdownStyle;
    GUIStyle changeDropdownStyle {
      get{
        if(_changeDropdownStyle == null){
          _changeDropdownStyle = new GUIStyle(defaultDropdownStyle);
          _changeDropdownStyle.fontStyle = FontStyle.Bold;
        }
        return _changeDropdownStyle;
      }
    }

    void CellGUI (Rect cellRect, TreeViewItem<SaveEditorTreeElement> item, MyColumns column, ref RowGUIArgs args)
    {
      // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
      CenterRectUsingSingleLineHeight(ref cellRect);

      switch (column)
      {
        case MyColumns.Name:
          {
            // Default icon and label
            args.rowRect = cellRect;

            if(item.data.setName != null && showControls){
              var offset = 25 * item.depth;
              var fieldRect = new Rect(offset, cellRect.y, cellRect.width - offset, cellRect.height);
              try{
                DrawGuiForValues(
                  fieldRect,
                  item.data.nameValue.GetType(),
                  ref item.data.nameValue,
                  ref item.data.originalNameValue,
                  ref item.data.m_Name,
                  ref item.data.isValidNameValue,
                  item.data.setName,
                  item.data
                );
              }catch(Exception e){
                Debug.LogError(e);
              }
            }else{
              base.RowGUI(args);
            }
          }
          break;

        case MyColumns.Value:
          {
            if (!showControls)
            {
              string value = "";
              if(item.data.valueType.IsGenericType){
                value = GetCSharpRepresentation(item.data.valueType, true);
              }else{
                value = item.data.value.ToString();
              }

              GUI.Label(cellRect, value);
            }
            else
            {
              cellRect.xMin += 5f; // When showing controls make some extra spacing
              if(item.data == null){
                Debug.LogWarning("Item with null data: " + item.displayName);
                return;
              }


              DrawGuiForValues(
                cellRect,
                item.data.valueType,
                ref item.data.value,
                ref item.data.originalValue,
                ref item.data.editorStrValue,
                ref item.data.isValidValue,
                item.data.setValue,
                item.data
              );

            }
          }
          break;

        case MyColumns.Controls:
          {
            var buttonStyle = new GUIStyle("miniButton");
            buttonStyle.fixedWidth = 20f;
            const float buttonMargin = 5f;
            int numButtons = 0;

            Rect NextButton(){
              var mv = buttonStyle.fixedWidth + buttonMargin;
              var ret = new Rect(cellRect.x + (mv * numButtons), cellRect.y, buttonStyle.fixedWidth, cellRect.height);
              numButtons++;
              return ret;
            }

            if(GUI.Button(NextButton(), new GUIContent("x", "Set to default"), buttonStyle)){
              RemoveItemsChildrenFromTree(item);
              var def = GetDefault(item.data.valueType);
              item.data.setValue(item.data.value, def, item.data);
              item.data.value = def;
              item.data.editorStrValue = item.data.value == null ? "null" : item.data.value.ToString();
            }

            //remove from array/list
            // var parentType = item.data.parent != null ? ((SaveEditorTreeElement)item.data.parent).valueType : null;
            if(item.data.removeValueFromParent != null){

              if(GUI.Button(NextButton(), new GUIContent("-", "Remove from collection"), buttonStyle)){
                item.data.removeValueFromParent(item.data);
                RemoveItemsChildrenFromTree(item);
                RemoveItemFromTree(item);
                if(item.data.needsSiblingRename){ RenameChildren(item.parent as TreeViewItem<SaveEditorTreeElement>); }
                Reload();
              }
            }
            //Add new element to collection
            if(item.data.addToCollection != null){
              if(GUI.Button(NextButton(), new GUIContent("+", "Add item to collection"), buttonStyle)){
                AddItemToCollection(item);
              }
            }
          }
          break;
      }
    }

    void DrawGuiForValues(
      Rect cellRect,
      Type dataType,
      ref object value,
      ref object originalValue,
      ref string editorStrValue,
      ref bool isValidValue,
      Action<object, object, SaveEditorTreeElement> setValue,
      SaveEditorTreeElement element
    ){

      var hasChanged = false;
      var funcStartingValue = value;

      if(value != null || originalValue != null){
        hasChanged =
          (value == null && originalValue != null)
          || (originalValue == null && value != null)
          || !value.Equals(originalValue);
      }

      var textFieldStyle = defaultGuiStyle;
      if(!isValidValue){
        textFieldStyle = errorGuiStyle;
      }else if(hasChanged){
        textFieldStyle = changedGuiStyle;
      }

      if(dataType == typeof(bool)){
        value = GUI.Toggle(cellRect, (bool)value, hasChanged ? "*" : "");
      }else if(dataType == typeof(string)){
        value = GUI.TextField(cellRect, (string)value, textFieldStyle);
      }else if(dataType.IsEnum){
        var enumValues = dataType.GetEnumValues();
        var enumNames = dataType.GetEnumNames();
        var _choiceIndex = Array.IndexOf(enumValues, value);
        if(_choiceIndex < 0){ _choiceIndex = 0; }

        GUILayout.BeginArea(cellRect);
        // EditorGUILayout.EnumPopup() a possibility?
        _choiceIndex = EditorGUILayout.Popup("", _choiceIndex, enumNames, hasChanged ? changeDropdownStyle : defaultDropdownStyle);
        GUILayout.EndArea();
        value = enumValues.GetValue(_choiceIndex);

      }else if(numberTypes.Contains(dataType)){

        if(editorStrValue == null){
          editorStrValue = value.ToString();
        }

        editorStrValue = GUI.TextField(cellRect, editorStrValue, textFieldStyle);

        var converter = TypeDescriptor.GetConverter(dataType);
        if (converter != null && converter.IsValid(editorStrValue)){
          value = converter.ConvertFromString(editorStrValue);
          isValidValue = true;
        }
        else{
          isValidValue = false;
        }

      }else if(dataType == typeof(BigInteger)){

        if(editorStrValue == null){
          editorStrValue = value.ToString();
        }

        editorStrValue = GUI.TextField(cellRect, editorStrValue, textFieldStyle);

        if(BigInteger.TryParse(editorStrValue, out var newInt)){
          value = newInt;
          isValidValue = true;
        }else{

          isValidValue = false;
        }

      }else{
        var label = GetCSharpRepresentation(dataType, true);
        if(value == null){
          label += " null";
        }else if(dataType.IsValueType){
          var toStr = value.ToString();
          if(!string.IsNullOrEmpty(toStr) && toStr != label){
            label += " " + toStr;
          }
        }

        GUI.Label(cellRect, label);
      }

      if(value != funcStartingValue){
        setValue(funcStartingValue, value, element);
      }
    }

    void AddItemToCollection(TreeViewItem<SaveEditorTreeElement> item){

      var decendents = treeModel.GetAllDescendantsIncludingHidden(item.id);

      //Initialize parent if needed
      if(item.data.value == null){
        var newVal = Activator.CreateInstance(item.data.valueType);
        item.data.setValue(item.data.value, newVal, item.data);
        item.data.value = newVal;
      }

      //create tree elements for the new data
      var newData = item.data.addToCollection(item.data);

      var treeElements = new List<SaveEditorTreeElement>();

      //regenerate all the elements for the current element, but then only use the ones that are new based on count
      treeGenerator.AddChildrenRecursive(treeElements, item.data, item.data.value, item.data.valueType);

      treeElements = treeElements.Where((t, i) => i >= decendents.Count).ToList();

      if(treeElements.Count > 1){
        //if we spawned a new class or something with children of its own we need to reparent its children to it first
        ReparentElementsAtIndex (
          treeElements.Skip(1).ToList().ConvertAll(c => (TreeElement)c),
          treeElements.First(),
          0
        );
        treeElements = treeElements.Take(1).ToList();
      }

      ReparentElementsAtIndex (
        treeElements.ConvertAll(c => (TreeElement)c),
        item.data,
        item.children != null ? item.children.Count : 0
      );

      if(item.data.needsSiblingRename){ RenameChildren(item); }
      Reload();
    }

    void RemoveItemFromTree(TreeViewItem<SaveEditorTreeElement> item){
      treeModel.RemoveElement(item.data);
      RemoveItem(item);
      item.parent.children.Remove(item);
    }

    void RemoveItemsChildrenFromTree(TreeViewItem<SaveEditorTreeElement> item){
      var decendents = treeModel.GetAllDescendantsIncludingHidden(item.id);
      treeModel.RemoveElements(decendents);
      RemoveChildrenRecursive(item);
    }

    void RenameChildren(TreeViewItem<SaveEditorTreeElement> item){
      if(item.children == null){ return; }
      var idx = 0;
      foreach(TreeViewItem<SaveEditorTreeElement> sibling in item.children) {
        sibling.data.name = idx.ToString();
        sibling.data.nameValue = idx;
        idx++;
      }
    }

    // Misc
    //--------

    protected override bool CanMultiSelect (TreeViewItem item) { return false; }

    public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
    {
      var columns = new[]
      {
        new MultiColumnHeaderState.Column
        {
          headerContent = new GUIContent("Name"),
          headerTextAlignment = TextAlignment.Left,
          width = 150,
          minWidth = 60,
          autoResize = false,
          allowToggleVisibility = false
        },
        new MultiColumnHeaderState.Column
        {
          headerContent = new GUIContent("Value"),
          headerTextAlignment = TextAlignment.Left,
          width = 70,
          minWidth = 60,
          autoResize = true
        },
        new MultiColumnHeaderState.Column
        {
          headerContent = new GUIContent("Controls"),
          headerTextAlignment = TextAlignment.Left,
          width = 70,
          minWidth = 60,
          autoResize = false
        }
      };

      Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

      var state = new MultiColumnHeaderState(columns);
      return state;
    }

    public static string GetCSharpRepresentation(Type t, bool trimArgCount)
    {
      if (t.IsGenericType)
      {
        var genericArgs = t.GetGenericArguments().ToList();

        return GetCSharpRepresentation(t, trimArgCount, genericArgs);
      }

      return t.Name;
    }

    static string GetCSharpRepresentation(Type t, bool trimArgCount, List<Type> availableArguments)
    {
      if (t.IsGenericType)
      {
        string value = t.Name;
        if (trimArgCount && value.IndexOf("`") > -1)
        {
          value = value.Substring(0, value.IndexOf("`"));
        }

        if (t.DeclaringType != null)
        {
          // This is a nested type, build the nesting type first
          value = GetCSharpRepresentation(t.DeclaringType, trimArgCount, availableArguments) + "+" + value;
        }

        // Build the type arguments (if any)
        string argString = "";
        var thisTypeArgs = t.GetGenericArguments();
        for (int i = 0; i < thisTypeArgs.Length && availableArguments.Count > 0; i++)
        {
          if (i != 0) argString += ", ";

          argString += GetCSharpRepresentation(availableArguments[0], trimArgCount);
          availableArguments.RemoveAt(0);
        }

        // If there are type arguments, add them with < >
        if (argString.Length > 0)
        {
          value += "<" + argString + ">";
        }

        return value;
      }

      return t.Name;
    }

    public static object GetDefault(Type type)
    {
      if(type.IsValueType)
      {
          return Activator.CreateInstance(type);
      }
      return null;
    }

    public static Type GetEnumeratedType(Type type)
    {
      // provided by Array
      var elType = type.GetElementType();
      if (null != elType) return elType;

      // otherwise provided by collection
      var elTypes = type.GetGenericArguments();
      if (elTypes.Length > 0) return elTypes[0];

      // otherwise is not an 'enumerated' type
      return null;
    }

  }

  static class MyExtensionMethods
  {
    public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
      if (ascending)
      {
        return source.OrderBy(selector);
      }
      else
      {
        return source.OrderByDescending(selector);
      }
    }

    public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
    {
      if (ascending)
      {
        return source.ThenBy(selector);
      }
      else
      {
        return source.ThenByDescending(selector);
      }
    }
  }
}
