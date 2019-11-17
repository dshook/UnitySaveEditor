using System;

namespace UnityEditor.SaveEditor
{

  [Serializable]
  internal class SaveEditorTreeElement : TreeElement
  {
    public Type valueType;
    public object originalValue;

    public object value;
    public string editorStrValue;
    public bool isValidValue = true;

    //oldValue, newValue, tree item
    public Action<object, object, SaveEditorTreeElement> setValue;

    public Action<SaveEditorTreeElement> removeValueFromParent;

    //returns new object created for collection
    public Func<SaveEditorTreeElement, object> addToCollection;

    //Setting the names of an item, such as for a dictionary key
    //oldKey, newKey, tree item
    public Action<object, object, SaveEditorTreeElement> setName;
    public object nameValue;
    public object originalNameValue;
    public bool isValidNameValue = true;

    //do the siblings need to be renamed after add or remove?
    public bool needsSiblingRename = false;

    public SaveEditorTreeElement (string name, int depth, int id) : base (name, depth, id)
    {
    }
  }
}
