using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class Chat : MonoBehaviour {

	private static int MESSAGE_LINE = 18;
	private static int CHAT_MEMBER_NUM = 2;

	// ネットワーク
	private TransportTCP m_transport;
	private string m_hostAddress = "";
	private const int m_port = 50765;
	private bool m_isServer = false;

	// 進行管理
	enum RoomState {
		SELECT_HOST = 0,
		JOINED,
		LEAVE,
		ERROR,
	}

	enum Command {
		NONE = 0,
		CREATE,
		JOIN,
		LEAVE,
	}
	private RoomState m_roomState = RoomState.SELECT_HOST;

	// メイン
	private List<string> m_messages;
	private string m_sendMessage;

	// GUI
	private Text m_messageTextBox;

	// Input
	private InputField inputField;

	// Use this for initialization
	void Start () {
		// Attach gameObject
		m_messageTextBox = GameObject.Find ("MessageText").GetComponent<Text> ();

		inputField = GameObject.Find ("InputField").GetComponent<InputField> ();

		// parameter initialize
		m_messages = new List<String> ();

		// Setup Network
		m_hostAddress = GetServerIPAddress ();

		GameObject go = new GameObject ("Network");
		m_transport = go.AddComponent<TransportTCP> ();

		m_transport.RegisterEventHandler (OnEventHandling);

	}

	// Update is called once per frame
	void Update () {
		switch (m_roomState) {
			case RoomState.SELECT_HOST:
				m_messages.Clear ();
				break;
			case RoomState.JOINED:
				UpdateChatting ();
				break;
			case RoomState.LEAVE:
				UpdateLeave ();
				break;
			case RoomState.ERROR:
				UpdateError ();
				break;
		}
		ShowMessage ();
	}

	void UpdateChatting () {
		byte[] buffer = new byte[1400];
		int receiveSize = m_transport.Receive (ref buffer, buffer.Length);
		if (receiveSize > 0) {
			string message = System.Text.Encoding.UTF8.GetString (buffer);
			AddMessage (ref m_messages, message);
		}
	}

	void UpdateLeave () {
		if (m_isServer == true) {
			m_transport.StopServer ();
		} else {
			if (m_transport.IsConnected ()) {
				m_transport.Disconnect ();
			}
		}
		m_roomState = RoomState.SELECT_HOST;
		m_messages.Clear ();
	}

	void UpdateError() {
		AddMessage(ref m_messages,"[System] Can't Connect!");
		m_roomState = RoomState.SELECT_HOST;
	}

	public void Send (string message) {
		message = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
		byte[] buffer = System.Text.Encoding.UTF8.GetBytes (message);

		AddMessage (ref m_messages, message);

		m_transport.Send (buffer, buffer.Length);
		m_sendMessage = "";
	}

	void AddMessage (ref List<string> messages, string str) {
		while (messages.Count >= MESSAGE_LINE) {
			messages.RemoveAt (0);
		}

		messages.Add (str);
	}

	// Room
	public void CreateRoom () {
		m_transport.StartServer (m_port, 1);
		m_roomState = RoomState.JOINED;
		m_isServer = true;
		AddMessage(ref m_messages, "[System] Created room!");
	}

	public void JoinRoom () {
		bool ret = m_transport.Connect (m_hostAddress, m_port);
		if (!ret) {
			m_roomState = RoomState.ERROR;
			return;
		}
		m_roomState = RoomState.JOINED;
	}
	public void OnEventHandling (NetEventState state) {
		switch (state.type) {
			case NetEventType.Connect:
				if (m_transport.IsServer ()) {
					AddMessage (ref m_messages, "[System] Client Joinned!");
				} else {
					AddMessage (ref m_messages, "[System] Start Chat!");
				}
				break;

			case NetEventType.Disconnect:
				if (m_transport.IsServer ()) {
					AddMessage (ref m_messages, "[System] Server Leaved");
				} else {
					AddMessage (ref m_messages, "[System] Client leaved");
				}
				break;
		}
	}

	// Network Util
	void OnApplicationQuit () {
		if (m_transport != null) {
			m_transport.StopServer ();
		}
	}

	// 端末のIPアドレスを取得.
	public string GetServerIPAddress () {

		string hostAddress = "";
		string hostname = Dns.GetHostName ();

		// ホスト名からIPアドレスを取得する.
		IPAddress[] adrList = Dns.GetHostAddresses (hostname);

		for (int i = 0; i < adrList.Length; ++i) {
			string addr = adrList[i].ToString ();
			string[] c = addr.Split ('.');

			if (c.Length == 4) {
				hostAddress = addr;
				break;
			}
		}

		return hostAddress;
	}

	// GUI
	void ShowMessage () {	
		m_messageTextBox.text = String.Join("\n", m_messages.ToArray());
	}

	// Input
	public void InputText () {
		string inputValue = inputField.text;
		if (inputValue.Trim() == "") {
			inputField.text = "";
			inputField.ActivateInputField ();
			return;
		}

		switch (getCommandType (inputValue)) {
			case Command.CREATE:
				CreateRoom ();
				break;
			case Command.JOIN:
				string hostAddress = getCommandBody (inputValue);
				if (hostAddress.Trim () != "") {
					m_hostAddress = hostAddress;
				}
				JoinRoom ();
				break;
			case Command.LEAVE:
				m_roomState = RoomState.LEAVE;
				break;
			default:
				Send (inputValue);
				break;
		}

		inputField.text = "";
		inputField.ActivateInputField ();
	}

	private Command getCommandType (string text) {
		Match m = Regex.Match (
			text,
			@"^/([a-zA-Z0-9]*)"
		);
		string cStr = m.Groups[1].Value;
		Command c = Command.NONE;
		try {
			c = (Command) Enum.Parse (typeof (Command), cStr, true);
		} catch (ArgumentException) {
			c = Command.NONE;
		}
		return c;
	}

	private string getCommandBody (string text) {
		Match m = Regex.Match (
			text,
			@"^/([a-zA-Z0-9]*)\s(.*)"
		);
		return m.Groups[2].Value;
	}
}
