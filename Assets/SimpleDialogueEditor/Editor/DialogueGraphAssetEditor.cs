using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SimpleDialogueEditor
{
    [CustomEditor(typeof(DialogueGraphAsset))]
    internal sealed class DialogueGraphAssetEditor : Editor
    {
        /// <summary>
        /// 在对话文件的检查器顶部添加编辑器打开入口。
        /// </summary>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            Button openButton = new Button(OpenDialogueEditor)
            {
                text = "打开对话编辑器"
            };
            openButton.style.marginBottom = 8;
            root.Add(openButton);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        /// <summary>
        /// 打开编辑器并切换到当前检查的对话文件。
        /// </summary>
        private void OpenDialogueEditor()
        {
            DialogueEditorWindow.OpenAsset((DialogueGraphAsset)target);
        }
    }
}
