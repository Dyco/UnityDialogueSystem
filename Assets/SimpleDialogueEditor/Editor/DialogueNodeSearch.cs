using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SimpleDialogueEditor
{
    /// <summary>
    /// 编辑器允许创建的节点类型。
    /// </summary>
    internal enum DialogueCreationType
    {
        /// <summary>创建文本对话节点。</summary>
        Text,

        /// <summary>创建选项对话节点。</summary>
        Option,

        /// <summary>创建结束节点。</summary>
        End
    }

    internal sealed class DialogueNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private DialogueGraphView graphView;
        private EditorWindow editorWindow;
        private Port sourcePort;
        private Vector2 nodePosition;

        /// <summary>
        /// 初始化节点搜索列表需要的编辑器上下文。
        /// </summary>
        public void Initialize(DialogueGraphView targetGraphView, EditorWindow targetEditorWindow)
        {
            graphView = targetGraphView;
            editorWindow = targetEditorWindow;
        }

        /// <summary>
        /// 在拖线结束位置打开节点创建列表。
        /// </summary>
        public void Open(Vector2 panelPosition, Port draggedPort)
        {
            sourcePort = draggedPort;
            nodePosition = graphView.contentViewContainer.WorldToLocal(panelPosition);
            Vector2 screenPosition = editorWindow.position.position + panelPosition;
            SearchWindow.Open(new SearchWindowContext(screenPosition), this);
        }

        /// <summary>
        /// 创建当前支持的节点类型列表。
        /// </summary>
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("添加节点"), 0),
                new SearchTreeEntry(new GUIContent("文本节点"))
                {
                    level = 1,
                    userData = DialogueCreationType.Text
                },
                new SearchTreeEntry(new GUIContent("选项节点"))
                {
                    level = 1,
                    userData = DialogueCreationType.Option
                }
            };

            if (sourcePort == null || sourcePort.direction == Direction.Output)
            {
                entries.Add(new SearchTreeEntry(new GUIContent("结束节点"))
                {
                    level = 1,
                    userData = DialogueCreationType.End
                });
            }

            return entries;
        }

        /// <summary>
        /// 在拖线落点创建选中的节点并自动连接。
        /// </summary>
        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (!(entry.userData is DialogueCreationType creationType))
            {
                return false;
            }

            graphView.CreateDialogueNode(nodePosition, creationType, sourcePort);
            sourcePort = null;
            return true;
        }
    }

    internal sealed class DialogueEdgeConnectorListener : IEdgeConnectorListener
    {
        private readonly DialogueNodeSearchProvider searchProvider;

        /// <summary>
        /// 创建用于处理端口连线与空白落点的监听器。
        /// </summary>
        public DialogueEdgeConnectorListener(DialogueNodeSearchProvider provider)
        {
            searchProvider = provider;
        }

        /// <summary>
        /// 将拖放到已有端口的连接加入画布。
        /// </summary>
        public void OnDrop(GraphView graphView, Edge edge)
        {
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            graphView.AddElement(edge);
            ((DialogueGraphView)graphView).NotifyGraphChanged();
        }

        /// <summary>
        /// 拖线落在空白处时打开节点创建列表。
        /// </summary>
        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            Port draggedPort = edge.output ?? edge.input;
            if (draggedPort != null)
            {
                searchProvider.Open(position, draggedPort);
            }
        }
    }

    internal sealed class DialoguePort : Port
    {
        /// <summary>
        /// 创建对话节点端口实例。
        /// </summary>
        private DialoguePort(Direction direction)
            : base(Orientation.Horizontal, direction, Capacity.Multi, typeof(bool))
        {
        }

        /// <summary>
        /// 创建支持空白落点搜索列表的对话端口。
        /// </summary>
        public static DialoguePort Create(Direction direction, IEdgeConnectorListener listener)
        {
            DialoguePort port = new DialoguePort(direction)
            {
                m_EdgeConnector = new EdgeConnector<Edge>(listener)
            };
            port.AddManipulator(port.m_EdgeConnector);
            ((EdgeDragHelper<Edge>)port.m_EdgeConnector.edgeDragHelper).resetPositionOnPan = false;
            return port;
        }
    }
}
