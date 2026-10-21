using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleDialogueEditor
{
    internal sealed class DialogueEditorWindow : EditorWindow
    {
        private const string LastAssetPathKey = "SimpleDialogueEditor.LastAssetPath";
        private const string DefaultAssetFolder = "Assets/MainResources/Dialogue";

        private DialogueGraphView graphView;
        private ObjectField dialogueAssetField;
        private TextField assetNameField;
        private ToolbarButton addNodeButton;
        private DialogueGraphAsset currentAsset;
        private DialogueGraphAsset pendingAsset;
        private bool saveScheduled;

        /// <summary>
        /// 打开简易对话编辑器窗口。
        /// </summary>
        [MenuItem("工具/简易对话编辑器")]
        private static void OpenWindow()
        {
            OpenAsset(null);
        }

        /// <summary>
        /// 打开对话编辑器，并加载指定的对话文件。
        /// </summary>
        public static void OpenAsset(DialogueGraphAsset asset)
        {
            DialogueEditorWindow window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent("对话编辑器");
            window.minSize = new Vector2(640f, 360f);

            if (asset != null)
            {
                if (window.graphView == null)
                {
                    window.pendingAsset = asset;
                }
                else if (window.currentAsset != asset)
                {
                    window.dialogueAssetField.SetValueWithoutNotify(asset);
                    window.SelectDialogueAsset(asset);
                }
            }

            window.Show();
            window.Focus();
        }

        /// <summary>
        /// 创建编辑器窗口的 UI Toolkit 界面。
        /// </summary>
        public void CreateGUI()
        {
            rootVisualElement.Clear();

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/SimpleDialogueEditor/Editor/DialogueEditor.uss");
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            graphView = new DialogueGraphView(this);
            graphView.Changed += ScheduleSave;
            rootVisualElement.Add(CreateToolbar());
            rootVisualElement.Add(graphView);

            if (pendingAsset != null)
            {
                dialogueAssetField.SetValueWithoutNotify(pendingAsset);
                SelectDialogueAsset(pendingAsset);
                pendingAsset = null;
            }
            else
            {
                LoadLastDialogueAsset();
            }
        }

        /// <summary>
        /// 创建窗口顶部工具栏。
        /// </summary>
        private Toolbar CreateToolbar()
        {
            Toolbar toolbar = new Toolbar();
            ToolbarButton createAssetButton = new ToolbarButton(CreateDialogueAsset)
            {
                text = "新建对话"
            };

            dialogueAssetField = new ObjectField("对话文件")
            {
                objectType = typeof(DialogueGraphAsset),
                allowSceneObjects = false
            };
            dialogueAssetField.AddToClassList("dialogue-asset-field");
            dialogueAssetField.RegisterValueChangedCallback(evt =>
                SelectDialogueAsset(evt.newValue as DialogueGraphAsset));

            assetNameField = new TextField("文件名")
            {
                isDelayed = true
            };
            assetNameField.AddToClassList("dialogue-name-field");
            assetNameField.RegisterValueChangedCallback(evt => RenameCurrentAsset(evt.newValue));
            assetNameField.SetEnabled(false);

            addNodeButton = new ToolbarButton(
                () => graphView.OpenNodeSearch(graphView.worldBound.center))
            {
                text = "新建节点"
            };
            addNodeButton.SetEnabled(false);

            toolbar.Add(createAssetButton);
            toolbar.Add(dialogueAssetField);
            toolbar.Add(assetNameField);
            toolbar.Add(addNodeButton);
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new Label("修改后自动保存"));
            return toolbar;
        }

        /// <summary>
        /// 创建新的对话数据资产并立即切换到该文件。
        /// </summary>
        private void CreateDialogueAsset()
        {
            EnsureDefaultAssetFolder();
            string path = GetNextDialogueAssetPath();
            DialogueGraphAsset asset = CreateInstance<DialogueGraphAsset>();
            asset.EnsureRequiredNodes();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            dialogueAssetField.value = asset;
        }

        /// <summary>
        /// 确保默认对话资源目录存在。
        /// </summary>
        private static void EnsureDefaultAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MainResources"))
            {
                AssetDatabase.CreateFolder("Assets", "MainResources");
            }

            if (!AssetDatabase.IsValidFolder(DefaultAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/MainResources", "Dialogue");
            }
        }

        /// <summary>
        /// 获取下一个递增编号的默认对话文件路径。
        /// </summary>
        private static string GetNextDialogueAssetPath()
        {
            int index = 1;
            string path;
            do
            {
                path = $"{DefaultAssetFolder}/Dialogue_{index:0000}.asset";
                index++;
            }
            while (AssetDatabase.LoadMainAssetAtPath(path) != null);

            return path;
        }

        /// <summary>
        /// 切换当前对话文件，并将其内容加载到画布。
        /// </summary>
        private void SelectDialogueAsset(DialogueGraphAsset asset)
        {
            SaveGraph();
            currentAsset = asset;
            addNodeButton.SetEnabled(currentAsset != null);
            assetNameField.SetEnabled(currentAsset != null);
            graphView.SetEnabled(currentAsset != null);

            if (currentAsset == null)
            {
                assetNameField.SetValueWithoutNotify(string.Empty);
                EditorPrefs.DeleteKey(LastAssetPathKey);
                graphView.ClearGraph();
                return;
            }

            string path = AssetDatabase.GetAssetPath(currentAsset);
            assetNameField.SetValueWithoutNotify(Path.GetFileNameWithoutExtension(path));
            EditorPrefs.SetString(LastAssetPathKey, path);
            if (currentAsset.EnsureRequiredNodes())
            {
                EditorUtility.SetDirty(currentAsset);
                AssetDatabase.SaveAssetIfDirty(currentAsset);
            }

            graphView.LoadGraph(currentAsset);
        }

        /// <summary>
        /// 将当前对话资产重命名为界面中输入的文件名。
        /// </summary>
        private void RenameCurrentAsset(string newFileName)
        {
            if (currentAsset == null)
            {
                return;
            }

            string currentPath = AssetDatabase.GetAssetPath(currentAsset);
            string currentName = Path.GetFileNameWithoutExtension(currentPath);
            string targetName = Path.GetFileNameWithoutExtension(newFileName.Trim());
            if (string.IsNullOrEmpty(targetName) || targetName == currentName)
            {
                assetNameField.SetValueWithoutNotify(currentName);
                return;
            }

            string error = AssetDatabase.RenameAsset(currentPath, targetName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"对话文件重命名失败：{error}");
                assetNameField.SetValueWithoutNotify(currentName);
                return;
            }

            string renamedPath = AssetDatabase.GetAssetPath(currentAsset);
            assetNameField.SetValueWithoutNotify(Path.GetFileNameWithoutExtension(renamedPath));
            EditorPrefs.SetString(LastAssetPathKey, renamedPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 加载上次打开的对话文件，便于继续编辑。
        /// </summary>
        private void LoadLastDialogueAsset()
        {
            string path = EditorPrefs.GetString(LastAssetPathKey);
            DialogueGraphAsset asset = AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(path);
            dialogueAssetField.SetValueWithoutNotify(asset);
            SelectDialogueAsset(asset);
        }

        /// <summary>
        /// 将同一帧内的连续修改合并为一次自动保存。
        /// </summary>
        private void ScheduleSave()
        {
            if (currentAsset == null || saveScheduled)
            {
                return;
            }

            saveScheduled = true;
            EditorApplication.delayCall += SaveGraph;
        }

        /// <summary>
        /// 将当前画布数据保存到选中的对话文件。
        /// </summary>
        private void SaveGraph()
        {
            EditorApplication.delayCall -= SaveGraph;
            saveScheduled = false;

            if (currentAsset == null || graphView == null)
            {
                return;
            }

            currentAsset.ReplaceData(graphView.GetNodeData(), graphView.GetConnectionData());
            EditorUtility.SetDirty(currentAsset);
            AssetDatabase.SaveAssetIfDirty(currentAsset);
        }

        /// <summary>
        /// 关闭窗口或重载脚本前保存尚未写入的修改。
        /// </summary>
        private void OnDisable()
        {
            if (graphView != null)
            {
                graphView.Changed -= ScheduleSave;
                graphView.Dispose();
            }

            SaveGraph();
        }
    }

    internal sealed class DialogueGraphView : GraphView
    {
        public event Action Changed;

        private readonly DialogueNodeSearchProvider searchProvider;
        private readonly DialogueEdgeConnectorListener edgeConnectorListener;
        private bool isLoading;

        /// <summary>
        /// 初始化节点画布及基础交互。
        /// </summary>
        public DialogueGraphView(EditorWindow editorWindow)
        {
            AddToClassList("dialogue-graph-view");

            searchProvider = ScriptableObject.CreateInstance<DialogueNodeSearchProvider>();
            searchProvider.hideFlags = HideFlags.HideAndDontSave;
            searchProvider.Initialize(this, editorWindow);
            edgeConnectorListener = new DialogueEdgeConnectorListener(searchProvider);

            GridBackground grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary>
        /// 打开创建列表，在指定的面板落点生成节点。
        /// </summary>
        public void OpenNodeSearch(Vector2 panelPosition)
        {
            searchProvider.Open(panelPosition, null);
        }

        /// <summary>
        /// 根据所选类型创建节点，并按需连接拖线来源端口。
        /// </summary>
        public DialogueNode CreateDialogueNode(
            Vector2 position,
            DialogueCreationType creationType,
            Port sourcePort = null)
        {
            DialogueNodeType nodeType = creationType == DialogueCreationType.End
                ? DialogueNodeType.End : DialogueNodeType.Normal;
            DialogueContentType contentType = creationType == DialogueCreationType.Option
                ? DialogueContentType.Option : DialogueContentType.Text;
            DialogueNodeData data = new DialogueNodeData(
                Guid.NewGuid().ToString("N"), nodeType,
                nodeType == DialogueNodeType.End ? "结束" : "新对话",
                string.Empty, position, contentType);
            DialogueNode node = new DialogueNode(data, NotifyChanged, edgeConnectorListener);
            AddElement(node);

            if (sourcePort != null)
            {
                ConnectCreatedNode(sourcePort, node);
            }

            NotifyChanged();
            return node;
        }

        /// <summary>
        /// 释放节点搜索列表使用的临时编辑器对象。
        /// </summary>
        public void Dispose()
        {
            ScriptableObject.DestroyImmediate(searchProvider);
        }

        /// <summary>
        /// 清空画布中的节点与连接。
        /// </summary>
        public void ClearGraph()
        {
            isLoading = true;
            foreach (Edge edge in edges.ToList())
            {
                RemoveElement(edge);
            }

            foreach (Node node in nodes.ToList())
            {
                RemoveElement(node);
            }

            isLoading = false;
        }

        /// <summary>
        /// 将对话文件中的节点与连接加载到画布。
        /// </summary>
        public void LoadGraph(DialogueGraphAsset asset)
        {
            ClearGraph();
            isLoading = true;

            Dictionary<string, DialogueNode> nodeMap = new Dictionary<string, DialogueNode>();
            foreach (DialogueNodeData nodeData in asset.Nodes)
            {
                DialogueNode node = new DialogueNode(
                    nodeData, NotifyChanged, edgeConnectorListener);
                nodeMap.Add(node.NodeId, node);
                AddElement(node);
            }

            foreach (DialogueConnectionData connection in asset.Connections)
            {
                if (!nodeMap.TryGetValue(connection.SourceNodeId, out DialogueNode source) ||
                    !nodeMap.TryGetValue(connection.TargetNodeId, out DialogueNode target))
                {
                    continue;
                }

                if (source.OutputPort != null && target.InputPort != null)
                {
                    AddElement(source.OutputPort.ConnectTo(target.InputPort));
                }
            }

            isLoading = false;
        }

        /// <summary>
        /// 获取画布上全部节点的可序列化数据。
        /// </summary>
        public List<DialogueNodeData> GetNodeData()
        {
            return nodes.ToList().OfType<DialogueNode>()
                .Select(node => node.CreateData()).ToList();
        }

        /// <summary>
        /// 获取画布上全部连接的可序列化数据。
        /// </summary>
        public List<DialogueConnectionData> GetConnectionData()
        {
            return edges.ToList().Select(edge => new DialogueConnectionData(
                ((DialogueNode)edge.output.node).NodeId,
                ((DialogueNode)edge.input.node).NodeId)).ToList();
        }

        /// <summary>
        /// 在画布右键菜单中提供节点创建入口。
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            Vector2 nodePosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("添加节点/文本节点", _ =>
                CreateDialogueNode(nodePosition, DialogueCreationType.Text));
            evt.menu.AppendAction("添加节点/选项节点", _ =>
                CreateDialogueNode(nodePosition, DialogueCreationType.Option));
            evt.menu.AppendAction("添加节点/结束节点", _ =>
                CreateDialogueNode(nodePosition, DialogueCreationType.End));
        }

        /// <summary>
        /// 只允许不同节点、不同方向的端口相互连接。
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(port =>
                port != startPort &&
                port.node != startPort.node &&
                port.direction != startPort.direction).ToList();
        }

        /// <summary>
        /// 响应节点移动、删除与连接变化，并请求自动保存。
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            NotifyChanged();
            return change;
        }

        /// <summary>
        /// 将拖线来源端口与新创建节点的对应端口连接。
        /// </summary>
        private void ConnectCreatedNode(Port sourcePort, DialogueNode node)
        {
            Port outputPort = sourcePort.direction == Direction.Output
                ? sourcePort
                : node.OutputPort;
            Port inputPort = sourcePort.direction == Direction.Input
                ? sourcePort
                : node.InputPort;

            if (outputPort != null && inputPort != null)
            {
                AddElement(outputPort.ConnectTo(inputPort));
            }
        }

        /// <summary>
        /// 在非加载状态下通知窗口画布数据已经改变。
        /// </summary>
        private void NotifyChanged()
        {
            if (!isLoading)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 通知窗口已有端口连接被加入画布。
        /// </summary>
        public void NotifyGraphChanged()
        {
            NotifyChanged();
        }
    }

    internal sealed class DialogueNode : Node
    {
        private readonly TextField nameField;
        private readonly TextField contentField;
        private DialogueContentType contentType;

        public string NodeId { get; }
        public DialogueNodeType NodeType { get; }
        public Port InputPort { get; }
        public Port OutputPort { get; }

        /// <summary>
        /// 创建包含名字、内容和输入输出端口的对话节点。
        /// </summary>
        public DialogueNode(
            DialogueNodeData data,
            Action changed,
            IEdgeConnectorListener edgeConnectorListener)
        {
            NodeId = data.Id;
            NodeType = data.NodeType;
            contentType = data.ContentType;
            title = GetNodeTitle();
            AddToClassList("dialogue-node");
            titleContainer.AddToClassList("dialogue-node__title");
            titleContainer.Add(CreateTypeField(changed));
            RefreshTypeStyle();

            if (NodeType != DialogueNodeType.Start)
            {
                InputPort = DialoguePort.Create(Direction.Input, edgeConnectorListener);
                InputPort.portName = "输入";
                inputContainer.Add(InputPort);
            }

            if (NodeType != DialogueNodeType.End)
            {
                OutputPort = DialoguePort.Create(Direction.Output, edgeConnectorListener);
                OutputPort.portName = "输出";
                outputContainer.Add(OutputPort);
            }

            if (NodeType == DialogueNodeType.Normal)
            {
                nameField = new TextField("名字");
                nameField.SetValueWithoutNotify(data.DialogueName);
                nameField.AddToClassList("dialogue-node__name");
                nameField.RegisterValueChangedCallback(_ => changed());

                contentField = new TextField("内容")
                {
                    multiline = true
                };
                contentField.SetValueWithoutNotify(data.Content);
                contentField.AddToClassList("dialogue-node__content");
                contentField.RegisterValueChangedCallback(_ => changed());

                extensionContainer.Add(nameField);
                extensionContainer.Add(contentField);
            }
            else
            {
                capabilities &= ~Capabilities.Deletable;
            }

            RefreshExpandedState();
            RefreshPorts();
            SetPosition(new Rect(data.Position, new Vector2(300f, 210f)));
        }

        /// <summary>
        /// 创建标题栏类型选择框，开始和结束节点只显示固定类型。
        /// </summary>
        private EnumField CreateTypeField(Action changed)
        {
            bool editable = NodeType == DialogueNodeType.Normal;
            Enum displayedType = editable ? (Enum)contentType : NodeType;
            EnumField typeField = new EnumField("Type", displayedType);
            typeField.AddToClassList("dialogue-node__type");
            typeField.SetEnabled(editable);
            if (editable)
            {
                typeField.RegisterValueChangedCallback(evt =>
                {
                    contentType = (DialogueContentType)evt.newValue;
                    title = GetNodeTitle();
                    RefreshTypeStyle();
                    changed();
                });
            }

            return typeField;
        }

        /// <summary>
        /// 根据节点流程身份和内容类型更新配色样式。
        /// </summary>
        private void RefreshTypeStyle()
        {
            bool normal = NodeType == DialogueNodeType.Normal;
            EnableInClassList("dialogue-node--text", normal && contentType == DialogueContentType.Text);
            EnableInClassList("dialogue-node--option", normal && contentType == DialogueContentType.Option);
            EnableInClassList("dialogue-node--start", NodeType == DialogueNodeType.Start);
            EnableInClassList("dialogue-node--end", NodeType == DialogueNodeType.End);

            Color color;
            if (NodeType == DialogueNodeType.Start)
            {
                color = new Color32(43, 94, 69, 255);
            }
            else if (NodeType == DialogueNodeType.End)
            {
                color = new Color32(119, 60, 60, 255);
            }
            else if (contentType == DialogueContentType.Option)
            {
                color = new Color32(94, 66, 119, 255);
            }
            else
            {
                color = new Color32(43, 78, 113, 255);
            }

            titleContainer.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            titleContainer.style.backgroundColor = color;
        }

        /// <summary>
        /// 根据节点当前状态创建可序列化数据。
        /// </summary>
        public DialogueNodeData CreateData()
        {
            return new DialogueNodeData(
                NodeId,
                NodeType,
                NodeType == DialogueNodeType.Normal ? nameField.value : title,
                NodeType == DialogueNodeType.Normal ? contentField.value : string.Empty,
                GetPosition().position,
                contentType);
        }

        /// <summary>
        /// 根据当前节点类型获取固定标题，不使用对话名字。
        /// </summary>
        private string GetNodeTitle()
        {
            if (NodeType == DialogueNodeType.Start)
            {
                return "开始";
            }

            if (NodeType == DialogueNodeType.End)
            {
                return "结束";
            }

            return contentType == DialogueContentType.Option ? "选项" : "文本";
        }
    }
}
