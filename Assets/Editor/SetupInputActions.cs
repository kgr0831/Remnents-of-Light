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
        // 일섬 차지(우클릭 홀드). Parry와 같은 버튼이지만 별도 액션 — Parry는 회피-카운터 확인키라
        // 의미가 다르고, F키 보조 바인딩이 일섬까지 발동시키면 안 되기 때문에 분리했다.
        map.AddAction("Charge", InputActionType.Button, "<Mouse>/rightButton");

        string path = "Assets/PlayerActions.inputactions";
        string json = asset.ToJson();
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
        Debug.Log("Created input actions at " + path);
    }
}
