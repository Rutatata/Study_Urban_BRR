// STUB (compile-check only): UnityEngine.InputSystem.UI (com.unity.inputsystem). Not functional.
using UnityEngine.EventSystems;
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : BaseInputModule
    {
        public void AssignDefaultActions() { }
        public InputActionAsset actionsAsset { get; set; }
        public InputActionReference point { get; set; } public InputActionReference leftClick { get; set; } public InputActionReference rightClick { get; set; }
        public InputActionReference middleClick { get; set; } public InputActionReference scrollWheel { get; set; } public InputActionReference move { get; set; }
        public InputActionReference submit { get; set; } public InputActionReference cancel { get; set; }
        public override void Process() { }
    }
}
