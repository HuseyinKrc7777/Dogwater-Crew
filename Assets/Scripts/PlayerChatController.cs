using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.Serialization;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerChatController : NetworkBehaviour
{
    NetworkList<ChatMessage> chatLog = new();
    NetworkVariable<ulong> messageIdCounter = new(1);

    [SerializeField] private TextMeshProUGUI chatWindow;
    [SerializeField] private TMP_InputField ChatInput;
    public static PlayerChatController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public struct ChatMessage : INetworkSerializable, IEquatable<ChatMessage>
    {
        public ulong SenderId;
        public ulong messageId;

        public FixedString64Bytes message;
        public FixedString64Bytes senderName;

        public ChatMessage(ulong SenderId, ulong messageId, FixedString64Bytes message,FixedString64Bytes senderName)
        {
            this.SenderId = SenderId;
            this.messageId = messageId;
            this.message = message;
            this.senderName = senderName;

        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
         where T : IReaderWriter
        {
            serializer.SerializeValue(ref SenderId);

            serializer.SerializeValue(ref messageId);
            serializer.SerializeValue(ref message);
            serializer.SerializeValue(ref senderName);

        }
        public bool Equals(ChatMessage other)
        {
            return messageId == other.messageId && message.Equals(other.message);
        }
    }
    public void TrySendMessage(string str)
    {
        NetworkObject localPlayer =
            NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

        SendChatMessageRpc((FixedString64Bytes)str,localPlayer.GetComponent<Player>().PlayerName.Value);
        ChatInput.text = "" ;
    }
    [Rpc(SendTo.Server)]
    public void SendChatMessageRpc(FixedString64Bytes message,FixedString64Bytes senderName, RpcParams rpcParams = default)
    {
        ChatMessage msg = new(rpcParams.Receive.SenderClientId, messageIdCounter.Value++, message,senderName);
        chatLog.Add(msg);
        Debug.LogError(chatLog.Count);
    }


    private void UpdateChat(NetworkListEvent<ChatMessage> changeEvent)
    {
        if (changeEvent.Type != NetworkListEvent<ChatMessage>.EventType.Add)
            return;

        ChatMessage msg = changeEvent.Value;
        chatWindow.text += $"{msg.senderName}: {msg.message}\n";
    }



    public string GetChat()
    {
        string chat = "";

        foreach (ChatMessage msg in chatLog)
        {
            chat += msg.senderName + " : " + msg.message + "\n";
        }
        return chat;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        chatWindow.text = GetChat();
        chatLog.OnListChanged += UpdateChat;
        ChatInput.onSubmit.AddListener(TrySendMessage);
        Canvas.ForceUpdateCanvases();       
        
    }
    public void Setup(Player playerObject)
    {
        
    }
    public override void OnNetworkDespawn()
    {
        chatLog.OnListChanged -= UpdateChat;
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
