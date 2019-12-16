using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace UnityEditor.SaveEditor
{

  class SaveEditorTreeGenerator
  {
    int IDCounter;
    object loadedGameData;

    public void SaveTree(string fullSavePath, TreeModel<SaveEditorTreeElement> treeModel){
      if(loadedGameData == null){
        Debug.LogError("Need to load save file first");
        return;
      }
      try{
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(fullSavePath);
        bf.Serialize(file, loadedGameData);
        file.Close();
        Debug.Log("Saved: " + fullSavePath);

        //mark all the tree nodes as unchanged after successful save
        foreach(var element in treeModel.GetData()){
          element.originalValue = element.value;
          element.originalNameValue = element.nameValue;
        }
      }catch(Exception e){
        Debug.LogError("Error Saving: " + e.Message);
      }
    }

    public List<SaveEditorTreeElement> GenerateTree(string fullSavePath)
    {
      BinaryFormatter bf = new BinaryFormatter();
      FileStream file = File.Open(fullSavePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      loadedGameData = bf.Deserialize(file);
      file.Close();


      IDCounter = 0;
      var treeElements = new List<SaveEditorTreeElement>();

      var root = new SaveEditorTreeElement("Root", -1, IDCounter);
      treeElements.Add(root);
      AddChildrenRecursive(treeElements, root, loadedGameData, loadedGameData.GetType());

      Debug.Log("Generated Tree with " + treeElements.Count + " elements");
      return treeElements;

    }

    public void AddChildrenRecursive(
      List<SaveEditorTreeElement> treeElements,
      SaveEditorTreeElement element,
      object data,
      Type typeSrc
    )
    {

      if(IDCounter > 15000){
        //recursive kill safeguard
        Debug.LogError("Max Tree Size Reached, Aborting");
        return;
      }

      if(typeSrc.IsPrimitive || typeSrc == typeof(string)){
        // Debug.Log("Hit primative " + typeSrc.Name);
        return;
      }

      var interfaces = typeSrc.GetInterfaces();

      if(typeSrc.IsArray){
        // Debug.Log("Adding Array");
        var idx = 0;
        if(data != null){
          foreach(object item in (Array)data) {
            var child = AddChild(treeElements, element, idx, item, item.GetType(),
              (old, val, titem) => { ((IList)data)[(int)titem.nameValue] = val; }
            );
            child.removeValueFromParent = (val) => { ((IList)data).Remove(val.value); };
            child.needsSiblingRename = true;
            AddChildrenRecursive(treeElements, child, item, MultiColumnTreeView.GetEnumeratedType(typeSrc));

            idx++;
          }
        }

      }else if(interfaces.Any(x => x.Name == "IDictionary")){
        // Debug.Log("Adding Dict " + MultiColumnTreeView.GetCSharpRepresentation(typeSrc, true));
        if(data != null){
          foreach(var item in ((IDictionary)data).Keys) {
            var value = ((IDictionary)data)[item];
            var child = AddChild(treeElements, element, item, value, value.GetType(),
              (old, val, titem) => { ((IDictionary)data)[titem.nameValue] = val; }
            );
            child.removeValueFromParent = (val) => { ((IDictionary)data).Remove(val.nameValue); };
            child.setName = (oldKey, newKey, treeItem) => {
              if(oldKey == newKey || oldKey.Equals(newKey)){ return; }
              var cData = (IDictionary)data;
              cData[newKey] = cData[oldKey];
              cData.Remove(oldKey);
              treeItem.name = newKey.ToString();
            };

            AddChildrenRecursive(treeElements, child, value, typeSrc.GetGenericArguments()[1]);
          }
        }

        element.addToCollection = (treeItem) => {
          var arguments = typeSrc.GetGenericArguments();
          Type keyType = arguments[0];
          Type valueType = arguments[1];
          var newKey = Activator.CreateInstance(keyType);
          var newVal = Activator.CreateInstance(valueType);
          ((IDictionary)treeItem.value)[newKey] = newVal;
          return newVal;
        };
      } else if(interfaces.Any(x => x.Name == "IList")){
        // Debug.Log("Adding IList");
        if(data != null){
          var idx = 0;
          foreach(object item in (IList)data) {
            var child = AddChild(treeElements, element, idx, item, item.GetType(),
              (old, val, titem) => { ((IList)data)[(int)titem.nameValue] = val; }
            );
            child.removeValueFromParent = (val) => { ((IList)data).Remove(val.value); };
            child.needsSiblingRename = true;
            AddChildrenRecursive(treeElements, child, item, MultiColumnTreeView.GetEnumeratedType(typeSrc));
            idx++;
          }
        }
        element.addToCollection = (treeItem) => {
          var val = Activator.CreateInstance(MultiColumnTreeView.GetEnumeratedType(element.valueType));
          ((IList)treeItem.value).Add(val);
          return val;
        };
        element.needsSiblingRename = true;

      } else if(typeSrc.IsGenericType && typeSrc.GetGenericTypeDefinition() == typeof(HashSet<>)) {

        if(data != null){
          var hs = (IEnumerable)data;
          foreach(object item in hs) {
            var child = AddChild(treeElements, element, item.GetHashCode(), item, item.GetType(),
              (old, val, titem) => { /*hs.Remove(old); hs.Add(val); */}
            );

            child.removeValueFromParent = (val) => {
              MethodInfo methodInfo = typeSrc.GetMethod("Remove");
              object[] parametersArray = new object[] {val.value};
              methodInfo.Invoke(data, parametersArray);
            };
            // child.needsSiblingRename = true;
            AddChildrenRecursive(treeElements, child, item, MultiColumnTreeView.GetEnumeratedType(typeSrc));
          }
        }
        element.addToCollection = (treeItem) => {
          var val = Activator.CreateInstance(MultiColumnTreeView.GetEnumeratedType(element.valueType));
          MethodInfo methodInfo = treeItem.valueType.GetMethod("Add");
          object[] parametersArray = new object[] {val};
          methodInfo.Invoke(treeItem.value, parametersArray);
          return val;
        };
      }else if(data != null && (typeSrc.IsClass || IsCustomValueType(typeSrc))){
        // Debug.Log("Adding Class");

        foreach (var prop in typeSrc.GetProperties())
        {
          if(!prop.CanRead || !prop.CanWrite){ continue; }
          if(prop.GetIndexParameters().Length > 0){ continue; }
          if(prop.PropertyType.IsAbstract){ continue; }

          var value = prop.GetValue(data);
          var child = AddChild(treeElements, element, prop.Name, value, prop.PropertyType,
            (old, val, titem) => { prop.SetValue(data, val); }
          );

          AddChildrenRecursive(treeElements, child, value, prop.PropertyType);
        }
        foreach (var field in typeSrc.GetFields())
        {
          if(field.IsLiteral){ continue; }
          if( IsTypeStatic(field.Attributes)){ continue; }

          var value = field.GetValue(data);
          SaveEditorTreeElement child;

          if(IsCustomValueType(typeSrc)){
            //TODO: have to do custom setting of values for structs that works
            var f = field;
            child = AddChild(treeElements, element, field.Name, value, field.FieldType,
              (old, val, titem) => {
                try{
                  f.SetValueDirect(__makeref(data), val);
                }catch(Exception e){
                  Debug.LogError(e);
                }
              }
            );
          }else{
            child = AddChild(treeElements, element, field.Name, value, field.FieldType,
              (old, val, titem) => {field.SetValue(data, val); }
            );
          }

          AddChildrenRecursive(treeElements, child, value, field.FieldType);
        }
      }
    }

    SaveEditorTreeElement AddChild(
      List<SaveEditorTreeElement> treeElements,
      TreeElement currentElement,
      object name,
      object value,
      Type valueType,
      Action<object, object, SaveEditorTreeElement> setValue
    ){
      var child = new SaveEditorTreeElement(name.ToString(), currentElement.depth + 1, ++IDCounter);
      child.originalValue = value;
      child.value = value;
      child.valueType = valueType;
      child.setValue = setValue;
      child.nameValue = name;
      child.originalNameValue = name;

      treeElements.Add(child);
      return child;
    }

    static bool IsCustomValueType(Type type) {
      return type.IsValueType && !type.IsEnum && !type.IsPrimitive && (type.Namespace == null || !type.Namespace.StartsWith("System."));
    }

    static bool IsTypeStatic(FieldAttributes attributes){
      return (attributes & FieldAttributes.Static) > 0;
    }
  }
}
