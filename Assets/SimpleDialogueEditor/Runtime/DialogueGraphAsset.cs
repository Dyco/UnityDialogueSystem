using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleDialogueEditor
{
    /// <summary>
    /// 对话节点的业务类型。
    /// </summary>
    public enum DialogueNodeType
    {
        /// <summary>普通对话节点。</summary>
        [InspectorName("普通")]
        Normal,

        /// <summary>对话开始节点。</summary>
        [InspectorName("开始")]
        Start,

        /// <summary>对话结束节点。</summary>
        [InspectorName("结束")]
        End
    }

    /// <summary>
    /// 普通对话节点的内容类型，与开始、结束等流程身份独立。
    /// </summary>
    public enum DialogueContentType
    {
        /// <summary>显示文本对话，兼容未设置内容类型的已有节点。</summary>
        [InspectorName("文本")]
        Text = 0,

        /// <summary>提供对话选项。</summary>
        [InspectorName("选项")]
        Option = 1
    }

    [CreateAssetMenu(fileName = "DialogueGraph", menuName = "对话/对话文件")]
    public sealed class DialogueGraphAsset : ScriptableObject
    {
        [SerializeField] private List<DialogueNodeData> nodes = new List<DialogueNodeData>();
        [SerializeField] private List<DialogueConnectionData> connections = new List<DialogueConnectionData>();

        public IReadOnlyList<DialogueNodeData> Nodes => nodes;
        public IReadOnlyList<DialogueConnectionData> Connections => connections;

        /// <summary>
        /// 使用编辑器画布中的最新数据替换当前数据。
        /// </summary>
        public void ReplaceData(
            List<DialogueNodeData> nodeData,
            List<DialogueConnectionData> connectionData)
        {
            nodes = nodeData;
            connections = connectionData;
        }

        /// <summary>
        /// 补齐对话文件必须包含的开始节点与结束节点。
        /// </summary>
        public bool EnsureRequiredNodes()
        {
            bool changed = false;
            if (!nodes.Exists(node => node.NodeType == DialogueNodeType.Start))
            {
                nodes.Add(new DialogueNodeData(
                    Guid.NewGuid().ToString("N"), DialogueNodeType.Start,
                    "开始", string.Empty, new Vector2(100f, 100f)));
                changed = true;
            }

            if (!nodes.Exists(node => node.NodeType == DialogueNodeType.End))
            {
                nodes.Add(new DialogueNodeData(
                    Guid.NewGuid().ToString("N"), DialogueNodeType.End,
                    "结束", string.Empty, new Vector2(600f, 100f)));
                changed = true;
            }

            return changed;
        }
    }

    [Serializable]
    public sealed class DialogueNodeData
    {
        [SerializeField] private string id;
        [SerializeField] private DialogueNodeType nodeType;
        [SerializeField] private DialogueContentType contentType;
        [SerializeField] private string dialogueName;
        [SerializeField, TextArea] private string content;
        [SerializeField] private Vector2 position;

        public string Id => id;
        public DialogueNodeType NodeType => nodeType;
        public DialogueContentType ContentType => contentType;
        public string DialogueName => dialogueName;
        public string Content => content;
        public Vector2 Position => position;

        /// <summary>
        /// 创建一条可序列化的对话节点数据。
        /// </summary>
        public DialogueNodeData(
            string id,
            DialogueNodeType nodeType,
            string dialogueName,
            string content,
            Vector2 position,
            DialogueContentType contentType = DialogueContentType.Text)
        {
            this.id = id;
            this.nodeType = nodeType;
            this.contentType = contentType;
            this.dialogueName = dialogueName;
            this.content = content;
            this.position = position;
        }
    }

    [Serializable]
    public sealed class DialogueConnectionData
    {
        [SerializeField] private string sourceNodeId;
        [SerializeField] private string targetNodeId;

        public string SourceNodeId => sourceNodeId;
        public string TargetNodeId => targetNodeId;

        /// <summary>
        /// 创建一条可序列化的节点连接数据。
        /// </summary>
        public DialogueConnectionData(string sourceNodeId, string targetNodeId)
        {
            this.sourceNodeId = sourceNodeId;
            this.targetNodeId = targetNodeId;
        }
    }
}
