using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

public class SetupInputActions {
    [MenuItem("Tools/Generate Player Actions")]
    public static void Run() {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        var map = asset.AddActionMap("Player");
        
        var moveAction = map.AddAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
            
        map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
        map.AddAction("Dash", InputActionType.Button, "<Keyboard>/shift");
        map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        map.AddAction("Parry", InputActionType.Button, "<Mouse>/rightButton");

        string path = "Assets/PlayerActions.inputactions";
        string json = asset.ToJson();
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
        Debug.Log("Created input actions at " + path);
    }
}
