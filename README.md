## Unity Save Editor

Simply put, this is an editor script for Unity that allows you to view and edit your save games that are saved to disk with the built in serialization model.

If your save function looks like this you're in luck!
```
      BinaryFormatter bf = new BinaryFormatter();
      FileStream file = File.Create(savePath);
      bf.Serialize(file, gameData);
      file.Close();
```

Currently you can view and edit Arrays, Lists, Dictionaries, classes, enums, and primative types.

Values can be reset to their default type value with the x control. New items can be added to collections with the + button on supported collections, and values in collections can be removed with the - control.

There are still some features yet to be added, so please check the issues for those and feel free to contribute too!

![In Action](https://res.cloudinary.com/dillonshook/image/upload/v1574031372/save_editor_xctbss.gif)

### Installation

Download the repository into an Editor folder your Unity project.  I have the directory structure set up here as `Assets/Scripts/Editor/SaveEditor` but `Assets/Editor/SaveEditor` will work too. Once you have it copied you should see the menu item to open it in Unity's window menu:

![Unity SaveEditor Menu](https://res.cloudinary.com/dillonshook/image/upload/v1589589987/SaveEditor_pvj7hk.jpg)
