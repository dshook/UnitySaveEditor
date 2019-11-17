using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.IO;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;

namespace UnityEditor.SaveEditor
{
  class SaveEditor : EditorWindow
  {
      [MenuItem("Window/Save Editor")]
      public static SaveEditor GetWindow()
      {
        var window = GetWindow<SaveEditor>();
        window.titleContent = new GUIContent("Save Editor");
        window.Focus();
        window.Repaint();
        return window;
      }

      string savePath = "";
      string extension = ".gd";


      int _choiceIndex = 0;
      string saveFileName;

      string fullSavePath{
        get{ return savePath + "/" + saveFileName; }
      }

      [NonSerialized] bool m_Initialized;
      // Serialized in the window layout file so it survives assembly reloading
      [SerializeField] TreeViewState m_TreeViewState;
      [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
      SearchField m_SearchField;
      MultiColumnTreeView m_TreeView;
      SaveEditorTreeGenerator treeGenerator = new SaveEditorTreeGenerator();

      void OnGUI()
      {
        TopGui();
        TreeView();
      }

      void TopGui(){
        var sectionMargin = 5;

        GUILayout.BeginArea (TopToolbarRect);

        GUILayout.BeginHorizontal();
          GUILayout.Label("Save Path");
          GUILayout.FlexibleSpace();
          savePath = GUILayout.TextField(savePath, GUILayout.Width(position.width - 150f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

          GUILayout.FlexibleSpace();
          var style = "miniButton";
          if(GUILayout.Button("Default", style, GUILayout.Width(60f))){
            savePath = Application.persistentDataPath;
          }
          if(GUILayout.Button("Open", style, GUILayout.Width(60f))){
            System.Diagnostics.Process.Start(savePath);
          }

        GUILayout.EndHorizontal();
        GUILayout.Space(sectionMargin);

        GUILayout.BeginHorizontal();
          GUILayout.Label("Extension");
          GUILayout.FlexibleSpace();
          extension = GUILayout.TextField(extension, GUILayout.Width(50f));
        GUILayout.EndHorizontal();
        GUILayout.Space(sectionMargin);

        //save list
        EditorGUILayout.BeginHorizontal();

          string[] _choices = new string[0];
          try{
            DirectoryInfo d = new DirectoryInfo(savePath);
            FileInfo[] Files = d.GetFiles("*." + extension.Replace(".", ""));
            _choices = Files.Select(f => f.Name).ToArray();
          }catch(Exception){ }

          if(_choices.Contains(saveFileName)){
            _choiceIndex = Array.IndexOf(_choices, saveFileName);
          }
          if(_choices.Length == 0){

            GUILayout.BeginHorizontal();
            GUILayout.Label("Save File");
            GUILayout.Label("No files found");

            GUILayout.EndHorizontal();
            saveFileName = "";
          }else{
            if (_choiceIndex < 0){ _choiceIndex = 0; }

            var newChoiceIndex = EditorGUILayout.Popup("Save File", _choiceIndex, _choices);
            if (newChoiceIndex < 0){ newChoiceIndex = 0; }

            bool changed = newChoiceIndex != _choiceIndex;
            _choiceIndex = newChoiceIndex;

            saveFileName = _choices[_choiceIndex];

            if(changed){
              InitIfNeeded(true);
            }
          }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(sectionMargin);

        GUILayout.BeginHorizontal();
          if(GUILayout.Button("Reload Save", GUILayout.Width(120f))){
            InitIfNeeded(true);
          }

          GUILayout.FlexibleSpace();
          if(GUILayout.Button("Save", GUILayout.Width(60f))){
            treeGenerator.SaveTree(fullSavePath, treeView.treeModel);
          }
          if(GUILayout.Button("Save As", GUILayout.Width(60f))){
            var saveAsFileName = EditorUtility.SaveFilePanel("New Save", savePath, "new_save", extension.Replace(".", ""));
            treeGenerator.SaveTree(saveAsFileName, treeView.treeModel);
          }

        GUILayout.EndHorizontal();
        GUILayout.Space(sectionMargin);

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        GUILayout.EndArea();
      }

      void TreeView(){
        if(string.IsNullOrEmpty(saveFileName)){
          GUILayout.Label("No save selected");
          return;
        }

        try{
          InitIfNeeded();
        }catch(Exception e){
          // Debug.LogWarning("Could not deserialize save: \n" + e.Message);
          GUILayout.BeginArea(multiColumnTreeViewRect);
          GUILayout.Label("Error deserializing save");
          GUILayout.Label(e.Message);
          GUILayout.Label(e.StackTrace);
          GUILayout.EndArea();
          return;
        }

        try{
          SearchBar (toolbarRect);
          DoTreeView (multiColumnTreeViewRect);
          BottomToolBar (bottomToolbarRect);
        }catch(ArgumentException){
          // this "Getting control 3's position in a group with only 3 controls when doing repaint"
          // error seems to be harmless and I can't find a way to get rid of it now so I'm just swallowing it
          // there is some weirdness about the tree redrawing the bottom controls now after a reload save action
          // that may be releated but it fixes itself after clicking on it.
        }
      }

      // Tree stuff

      Rect TopToolbarRect
      {
        get { return new Rect(20f, 20f, position.width - 40f, 140f); }
      }

      Rect multiColumnTreeViewRect
      {
        get { return new Rect(20, 30 + TopToolbarRect.height, position.width-40, position.height-210); }
      }

      Rect toolbarRect
      {
        get { return new Rect (20f, 10f + TopToolbarRect.height, position.width-40f, 20f); }
      }

      Rect bottomToolbarRect
      {
        get { return new Rect(20f, position.height - 28f, position.width - 40f, 16f); }
      }

      public MultiColumnTreeView treeView
      {
        get { return m_TreeView; }
      }

      void InitIfNeeded (bool force = false)
      {
        if (m_Initialized && !force) {
          return;
        }

        // Check if it already exists (deserialized from window layout file or scriptable object)
        if (m_TreeViewState == null || force)
          m_TreeViewState = new TreeViewState();

        bool firstInit = m_MultiColumnHeaderState == null;
        var headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
        if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
          MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
        m_MultiColumnHeaderState = headerState;

        var multiColumnHeader = new MyMultiColumnHeader(headerState);
        if (firstInit || force)
          multiColumnHeader.ResizeToFit ();

        var treeModel = new TreeModel<SaveEditorTreeElement>(GetData());

        m_TreeView = new MultiColumnTreeView(m_TreeViewState, multiColumnHeader, treeModel, treeGenerator);

        m_SearchField = new SearchField();
        m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

        m_Initialized = true;
      }

      IList<SaveEditorTreeElement> GetData ()
      {
        return treeGenerator.GenerateTree(fullSavePath);
      }

      void SearchBar (Rect rect)
      {
        treeView.searchString = m_SearchField.OnGUI (rect, treeView.searchString);
      }

      void DoTreeView (Rect rect)
      {
        m_TreeView.OnGUI(rect);
      }

      void BottomToolBar (Rect rect)
      {
        GUILayout.BeginArea (rect);

        using (new EditorGUILayout.HorizontalScope ())
        {

          var style = "miniButton";
          if (GUILayout.Button("Expand All", style))
          {
            treeView.ExpandAll ();
          }

          if (GUILayout.Button("Collapse All", style))
          {
            treeView.CollapseAll ();
          }

          GUILayout.FlexibleSpace();

          if (GUILayout.Button("values <-> controls", style))
          {
            treeView.showControls = !treeView.showControls;
          }
        }

        GUILayout.EndArea();
      }

    internal class MyMultiColumnHeader : MultiColumnHeader
    {

      public MyMultiColumnHeader(MultiColumnHeaderState state) : base(state)
      {
        canSort = false;
      }

      protected override void ColumnHeaderGUI (MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
      {
        // Default column header gui
        base.ColumnHeaderGUI(column, headerRect, columnIndex);
      }
    }
  }
}