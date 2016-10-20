// before unity 5.0
#define USE_OLD_UNITY_REPORTER

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Mail;    
using System.Net.Mime; 
using System.Net.Security;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

public enum eCrashWriteType
{
	EWRITEOFF = 0,
	EWRITEMAIL,
	EWRITEFILE,
	EWRITESERVER, //not working
	EWRITEGAv3 //google analytics
};

[System.Flags]
public enum eExceptionType
{
	None = 0,
	Log = 1,
	Warning = 2,
	Assert = 4,
	Error = 8,
	Exception = 16
};

[Serializable]
public class CrashReporter
{
	static bool m_bCrashCatched = false;
	public GameObject m_goMonoBehaviourObject;
	public eCrashWriteType m_eWriteMode = eCrashWriteType.EWRITEOFF;
	public bool m_bScreenShotSupport = true;
	public string m_strBuild_version = "1.0";
	public string m_strProjectName;
	public string m_strMailingList;
	public bool m_bUpdateWaitServerInfo = false;

	StreamWriter m_swWriter;
	int m_iExceptionCount = 0;
	eExceptionType	m_tCrashReporterLevel = eExceptionType.Error | eExceptionType.Exception;
	LogType m_tExceptionType = LogType.Log;
	int m_iMaxLogCount = 30;
	class LogStruct	{
		public LogType type;
		public string buffer;
		public string stacktrace;
	};

	class UserInfo	{
		public string userID;	//membership id
		public string userUID;	//gameserver game id
		public string nickname;	//player name
		public string teamname;	//player team or caractor type
	}

	class SmtpMailInfo	{
		public SmtpMailInfo(string id, string pwd, string smtp, bool ssl){
			userID = id;
			userPwd = pwd;
			smtpHost = smtp;
			sslEnable = ssl;
		}
		public string userID;
		public string userPwd;
		public string smtpHost;
		public bool sslEnable;
	}

	class ServerInfo	{
		public ServerInfo(string serverHost){
			SetServerHost(serverHost);
			nSmtpInfoIndex = 0;

			smtpList = new List<SmtpMailInfo>();
		}

		public string host;

		int nSmtpInfoIndex;
		List<SmtpMailInfo> smtpList;

		public void SetServerHost(string serverHost)
		{
			host = serverHost;
		}

		public SmtpMailInfo GetCurrentSmtp()	{

			if(smtpList.Count > nSmtpInfoIndex)
				return smtpList[nSmtpInfoIndex];

			return null;
		}

		public void SetNextSmtp()	{
			nSmtpInfoIndex++;
		}

		public void ResetSmtpIndex()	{
			nSmtpInfoIndex=0;
		}

		public void ClearSmtpList()	{
			smtpList.Clear();
		}

		public void AddSmtp(SmtpMailInfo smtp)	{
			ResetSmtpIndex();
			smtpList.Add(smtp);
		}
	}

	List<LogStruct> m_listLogBuffer = new List<LogStruct>();
	List<LogStruct> m_listLogBufferThread = new List<LogStruct>();

	UserInfo m_stUserInfo = new UserInfo();

	ServerInfo m_stServerInfo = new ServerInfo("");
	public void StartCrashReporter(GameObject go, string projectname = "", eCrashWriteType type = eCrashWriteType.EWRITEMAIL, string clientVersion="", string gmailID = "", string gmailPWD = "", string mailingList = "", eExceptionType level = eExceptionType.Exception)
	{

		m_eWriteMode = type;
		m_tCrashReporterLevel = level;

		#if UNITY_EDITOR
		m_eWriteMode = eCrashWriteType.EWRITEFILE;
		#endif
		if (m_eWriteMode == eCrashWriteType.EWRITEOFF)
			return;

		if (go.GetComponent<MonoBehaviour> () == null) {
			throw new Exception ("monobehaviour object not set : for use coroutine works");
			return;
		}

		m_bCrashCatched = false;
		UnityEngine.Debug.Log ("CrashReporter Start!!! Mode:" + m_eWriteMode.ToString());
		#if !UNITY_5
		Application.RegisterLogCallback (HandleException);
		Application.RegisterLogCallbackThreaded (HandleExceptionThread);
		#else
		Application.logMessageReceived += HandleException ;
		Application.logMessageReceivedThreaded += HandleExceptionThread ;
		#endif

		m_goMonoBehaviourObject = go;
		if(projectname.Length > 0)
			m_strProjectName = projectname;

		if(clientVersion.Length > 0)
			m_strBuild_version = clientVersion;

		if(gmailID.Length > 0)	{
			m_stServerInfo.AddSmtp(new SmtpMailInfo(gmailID,gmailPWD,"smtp.gmail.com",true));
		}

		if (mailingList.Length > 0)
			m_strMailingList = mailingList;

		m_stUserInfo.userID = "";
		m_stUserInfo.userUID = "";
		m_stUserInfo.nickname = "";
		m_stUserInfo.teamname = "";

		Routine (UpdateCrashRepoter ());
	}

	public void Finish()
	{

	}

	string GetMailingList()
	{
		string mailList = "";
		if (m_strMailingList.Length > 0 && m_strMailingList.IndexOf (";") != -1) {
			string[] sliceList = m_strMailingList.Split (';');
			for (int i = 0; i < sliceList.Length; i++) {
				mailList+=sliceList [i] + ",";
			}
		} else if (m_strMailingList.Length > 0) {
			mailList = m_strMailingList;
		}
		return mailList;
	}

	ArrayList GetSenderList()
	{
		ArrayList senders = new ArrayList();

		SmtpMailInfo info = null;
		while((info = m_stServerInfo.GetCurrentSmtp()) != null)	
		{
			Hashtable item = new Hashtable();
			item["id"] = info.userID;
			item["pwd"] = info.userPwd;
			item["smtp"] = info.smtpHost;
			item["ssl"] = info.sslEnable;
			senders.Add(item);

			m_stServerInfo.SetNextSmtp();
		}
		m_stServerInfo.ResetSmtpIndex();

		return senders;
	}

	IEnumerator SendDebugToServer (LogType type, string trace, string unreportedMessageBody = "")
	{

		if (m_bCrashCatched != true) {
			m_bCrashCatched = true;
			string function = trace;
			AnalyticsImplement(type,trace);

			UnityEngine.Debug.Log ("SendDebugToServer " );

			string mailList = GetMailingList();

			Hashtable data = new Hashtable();
			data["subject"] = "["+ m_strProjectName + " CrashReport - " + type.ToString() + " #" + m_strBuild_version.ToString() + " ] " + m_stUserInfo.teamname + " #" + DateTime.Now;
			data["text"] = unreportedMessageBody.Length>0? unreportedMessageBody : MakeMassageHeader(BufferToText());
			data["reciver"] = mailList;
			data["from"] = "no-replay";
			data["sender"] = GetSenderList();

			Debug.Log(SG.MiniJsonExtensions.toJson(GetSenderList()));

			if(unreportedMessageBody.Length <= 0)
				WriteFileLog(data["text"].ToString(), trace, type == LogType.Exception);

			ScreenShotRoutine((attachmentPath)=>{

				if(File.Exists(attachmentPath))
				{
					byte[] imageBytes = File.ReadAllBytes(attachmentPath);
					// Convert byte[] to Base64 String

					if(imageBytes.Length>0)
					{
						string base64String = Convert.ToBase64String(imageBytes);
						data["raw"] = base64String;
						Debug.Log("file save success = " + attachmentPath);
					}
				}

				string base_data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(SG.MiniJsonExtensions.toJson(data)));

				#if USING_GET
				WWW www = new WWW (m_strPostURL+"?data="+base_data);
				#else
				WWWForm form = new WWWForm();
				form.AddField("data",base_data);

				WWW www = new WWW (m_stServerInfo.host, form);
				#endif
				Routine(WaitForRequest(www,(msg)=>{
					Debug.Log(msg);
					FinalWorking (unreportedMessageBody.Length > 0);
				},()=>{
					Debug.Log("fail");
					FinalWorking (unreportedMessageBody.Length > 0);
				}));
			});
		}
		yield return null;
	}


	IEnumerator SendDebugToFile (LogType type, string trace)
	{
		Debug.Log ("SendDebugToFile");

		ScreenShotRoutine((attachmentPath)=>{

			WriteFileLog(BufferToText(), trace, type == LogType.Exception);
			FinalWorking ();
		});
		yield return null;
	}

	void ScreenShotRoutine(Action<string> callback)
	{
		if(m_bScreenShotSupport==false)
		{
			callback("");
			return;
		}


		if(File.Exists(GetScreenShotFullPath()))
			File.Delete(GetScreenShotFullPath());

		Routine (ScreenShot (), ()=>{

			if(File.Exists(GetScreenShotFullPath()))
			{
				callback(GetScreenShotFullPath());
			}else
				callback("");
		});
	}

	string GetScreenShotName()
	{
		#if UNITY_EDITOR
		return Application.persistentDataPath+"/exception.png";
		#else
		return "exception.png";
		#endif
	}

	string GetScreenShotFullPath()
	{
		return Application.persistentDataPath+"/exception.png";
	}

	IEnumerator ScreenShot()
	{
		UnityEngine.Debug.Log ("ScreenShot " );

		Application.CaptureScreenshot (GetScreenShotName());

		yield return new WaitForSeconds (1);

		try {
			FileInfo info = new FileInfo (GetScreenShotFullPath());
			UnityEngine.Debug.Log ("ScreenShot " + info.ToString () + " size : " + info.Length);	
		} catch {

		}
	}

	string GetFirstFunctionName(string trace)
	{
		string function = "[Exception] ";
		if (trace.IndexOf (" (") != -1) {

			bool isAssert = trace.Contains("SGUtil.Assert")==true?true:false;

			string[] line = trace.Split('\n');
			for (int i = 0; i < line.Length; i++) {
				string lineString = line[i];
				UnityEngine.Debug.Log("Line : " + lineString);
				if(isAssert)
				{
					function = "[SGUtil.Assert] ";
					isAssert = false;
					continue;
				}
				if(lineString.IndexOf(" (")!=-1)
					function += lineString.Substring(0, lineString.IndexOf(" ("));
				else if(lineString.IndexOf("(")!=-1)
					function += lineString.Substring(0, lineString.IndexOf("("));
				break;

			}
		}
		return function;
	}

	IEnumerator SendDebugToMail(LogType type, string trace, string gmailID = "", string gmailPwd = "", string unreportedMessageBody = "")
	{
		if (m_bCrashCatched != true) {
			m_bCrashCatched = true;
			string function = trace;
			AnalyticsImplement(type,trace);

			UnityEngine.Debug.Log ("SendDebugToMail " );
			MailMessage mail = new MailMessage ();

			mail.From = new MailAddress("CrashReporter");

			if (m_strMailingList.Length > 0 && m_strMailingList.IndexOf (";") != -1) {
				string[] sliceList = m_strMailingList.Split (';');
				for (int i = 0; i < sliceList.Length; i++) {
					mail.To.Add (sliceList [i]);
					//				UnityEngine.Debug.Log(sliceList[i]);
				}
			} else if (m_strMailingList.Length > 0) {
				mail.To.Add (m_strMailingList);
			} else {
				yield return null;
			}

			mail.Subject = "["+ m_strProjectName + " CrashReport - " + type.ToString() + " #" + m_strBuild_version.ToString() + " ] " + m_stUserInfo.teamname + " #" + DateTime.Now;

			mail.Body = unreportedMessageBody.Length > 0 ? unreportedMessageBody : MakeMassageHeader(BufferToText());


			ScreenShotRoutine( (attachmentPath)=>{

				if(unreportedMessageBody.Length <= 0)
					WriteFileLog(mail.Body, trace, type == LogType.Exception);
				try {
					if(File.Exists(attachmentPath))
					{
						Attachment inline = new Attachment(attachmentPath);
						string contentID = Path.GetFileName(attachmentPath).Replace(".", "") + "@zofm";
						inline.ContentDisposition.Inline = true;
						inline.ContentDisposition.DispositionType = DispositionTypeNames.Inline;
						inline.ContentId = contentID;
						inline.ContentType.MediaType = "image/png";
						inline.ContentType.Name = Path.GetFileName(attachmentPath);

						mail.Attachments.Add(inline);
					}

					SmtpClient smtpServer = new SmtpClient();
					smtpServer.Host = m_stServerInfo.GetCurrentSmtp().smtpHost;
					smtpServer.Port = 587;
					smtpServer.EnableSsl = m_stServerInfo.GetCurrentSmtp().sslEnable;
					smtpServer.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
					smtpServer.UseDefaultCredentials = false;
					smtpServer.Credentials = new System.Net.NetworkCredential (gmailID, 
						gmailPwd) as ICredentialsByHost;

					ServicePointManager.ServerCertificateValidationCallback = 
						delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
						return true;
					};

					smtpServer.Send(mail);
					UnityEngine.Debug.Log("send finish");

					FinalWorking(unreportedMessageBody.Length > 0);

				} catch (Exception ex) {
					Debug.Log ("Can't send crashreporter mail, smtp sending error : " + ex.Message);

					SmtpMailInfo info = m_stServerInfo.GetCurrentSmtp();
					if(info!=null)
					{
						m_stServerInfo.SetNextSmtp();
						m_bCrashCatched = false;
						Routine(SendDebugToMail(type, trace, info.userID, info.userPwd));
					}
				}
			});
		}

		yield return null;
	}

	void SendDebugToGoogleAnalytics(LogType type, string trace)
	{
		if (m_bCrashCatched != true) {
			m_bCrashCatched = true;

			UnityEngine.Debug.Log("send crash report to google");
			string function = trace;
			AnalyticsImplement(type,trace);

			Routine(SendDebugToFile(type, trace));
		}
	}

	void ResetBufferToLimit()
	{
		while (m_listLogBuffer.Count > m_iMaxLogCount) {
			m_listLogBuffer.RemoveAt(0);
		}
	}

	void HandleException(string condition, string stackTrace, LogType type)
	{
		string sep = "------------------------------------------------------------------------------\r\n";

		LogStruct st = new LogStruct{type=type,buffer=sep + "Type : " + type.ToString () + "\r\nTime : " + Time.realtimeSinceStartup.ToString () + "\r\nCondition : " + condition,
			stacktrace=stackTrace};
		m_listLogBuffer.Add (st);

		ResetBufferToLimit ();

		if(((int)m_tCrashReporterLevel & (int)Enum.Parse(typeof(eExceptionType), type.ToString())) != 0)
		{
			m_iExceptionCount++;
			m_tExceptionType = type;
		}
	}

	void AddThreadBuffer(LogStruct st)
	{
		lock( m_listLogBufferThread )
		{
			m_listLogBufferThread.Add (st);

			while (m_listLogBufferThread.Count > m_iMaxLogCount) {
				m_listLogBufferThread.RemoveAt(0);
			}
		}

	}

	void HandleExceptionThread(string condition, string stackTrace, LogType type)
	{
		string sep = "------------------------------------------------------------------------------\r\n";

		LogStruct st = new LogStruct{type=type,buffer=sep + "Type : " + type.ToString () + " Time : " + Time.realtimeSinceStartup.ToString () + "\r\n Condition : " + condition,
			stacktrace=stackTrace};
		AddThreadBuffer (st);
		if(((int)m_tCrashReporterLevel & (int)Enum.Parse(typeof(eExceptionType), type.ToString())) != 0)
		{
			m_iExceptionCount++;
			m_tExceptionType = type;
		}
	}

	string BufferToText()
	{
		string stringText = "";

		for (int i = 0; i < m_listLogBuffer.Count; i++) {
			stringText += m_listLogBuffer[(m_listLogBuffer.Count-1) - i].buffer.ToString();
			stringText += "\r\nStack: " + m_listLogBuffer[(m_listLogBuffer.Count-1) - i].stacktrace.ToString();

		}

		return stringText;
	}

	string MakeMassageHeader(string body)
	{
		/*
			Textures
			Meshes
			Materials
			Animations
			Audio
			Object Count 
		*/

		long textureSize = 0;
		long meshSize = 0;
		long materialSize = 0;
		long animationSize = 0;
		long audioSize = 0;
		long totalSize = System.GC.GetTotalMemory (true);

		UnityEngine.Object[] textures = Resources.FindObjectsOfTypeAll(typeof(Texture));
		foreach (Texture t in textures)
			textureSize += Profiler.GetRuntimeMemorySize (t);
		UnityEngine.Object[] meshs = Resources.FindObjectsOfTypeAll(typeof(Mesh));
		foreach (Mesh t in meshs)
			meshSize += Profiler.GetRuntimeMemorySize (t);
		UnityEngine.Object[] materials = Resources.FindObjectsOfTypeAll(typeof(Material));
		foreach (Material t in materials)
			materialSize += Profiler.GetRuntimeMemorySize (t);

		UnityEngine.Object[] anims = Resources.FindObjectsOfTypeAll(typeof(AnimationClip));
		foreach (AnimationClip t in anims)
			animationSize += Profiler.GetRuntimeMemorySize (t);

		UnityEngine.Object[] audios = Resources.FindObjectsOfTypeAll(typeof(AudioClip));
		foreach (AudioClip t in audios)
			audioSize += Profiler.GetRuntimeMemorySize (t);


		string msg = "\r\n\r\n------------------------------------------------------------------------------\r\n";
		msg += string.Format ("UserInfomation\r\n\r\nUserID:{0}, UID:{1}, TeamName:{2}, TeamIdent:{3}", 
			m_stUserInfo.userID.Length>0?m_stUserInfo.userID:"none",
			m_stUserInfo.userUID.Length>0?m_stUserInfo.userUID:"none",
			m_stUserInfo.nickname.Length>0?m_stUserInfo.nickname:"none",
			m_stUserInfo.teamname.Length>0?m_stUserInfo.teamname:"none"
		);
		msg += "\r\n\r\n------------------------------------------------------------------------------\r\n";

		msg += string.Format
			(
				//				"System Infomation\r\n\r\nModel:{0}, Name:{1}, Type:{2}, Ident:{3}\nSystem:{4}, Lang:{5}, MemSize:{6}, ProcCount:{7}, ProcType:x {8}\nScreen:{9}x{10}, DPI:{11}dpi, FullScreen:{12}, {13}, {14}, vmem: {15}, Fill: {16} Max Texture: {17}\n\nScene {18}, Unity Version {19}",
				"System Infomation\r\n\r\nModel:{0}, Name:{1}, Type:{2}\nSystem:{3}, Lang:{4}, MemSize:{5}, ProcCount:{6}, ProcType:x {7}\nScreen:{8}x{9}, DPI:{10}dpi, FullScreen:{11}, {12}, {13}, vmem: {14}, Fill: {15} Max Texture: {16}\n\nScene {17}, Unity Version {18}",
				SystemInfo.deviceModel,
				SystemInfo.deviceName,
				SystemInfo.deviceType,
				//				SystemInfo.deviceUniqueIdentifier,

				SystemInfo.operatingSystem,
				"",//Localization.language,
				SystemInfo.systemMemorySize,
				SystemInfo.processorCount,
				SystemInfo.processorType,

				Screen.currentResolution.width,
				Screen.currentResolution.height,
				Screen.dpi,
				Screen.fullScreen,
				SystemInfo.graphicsDeviceName,
				SystemInfo.graphicsDeviceVendor,
				SystemInfo.graphicsMemorySize,
				SystemInfo.graphicsPixelFillrate,
				SystemInfo.maxTextureSize,

				Application.loadedLevelName,
				Application.unityVersion
			);

		msg += "\r\n\r\n------------------------------------------------------------------------------\r\n";
		msg += string.Format ("Memory Status\r\n\r\nTotal:{0}Bytes\r\nTexture:{1}Bytes\r\nMash:{2}Bytes\r\nMaterial:{3}Bytes\r\nAnimation:{4}\r\nAudio:{5}\r\n",
			totalSize,
			textureSize,
			meshSize,
			materialSize,
			animationSize,
			audioSize);
		msg += "\r\n\r\n------------------------------------------------------------------------------\r\n";
		msg += "Log & Stack Trace\r\n\r\n";
		msg += body;

		return msg;
	}

	static void SomethingReallyBadHappened ()
	{
		//NB: Try and recover or fail gracefully here.
	}

	IEnumerator UpdateCrashRepoter()
	{
		while (true) {
			yield return new WaitForSeconds(1);

			//be sure no body else take control of log 
			#if !UNITY_5
			Application.RegisterLogCallback (HandleException);
			Application.RegisterLogCallbackThreaded (HandleExceptionThread);
			#else

			#endif

			if( m_listLogBufferThread.Count > 0 )
			{
				lock( m_listLogBufferThread )
				{
					for( int i = 0 ; i < m_listLogBufferThread.Count ; i++ )
					{
						m_listLogBuffer.Add (m_listLogBufferThread[i]);
					}
					m_listLogBufferThread.Clear();
				}
			}

			if (m_iExceptionCount > 0) {
				m_iExceptionCount = 0;
				UnityEngine.Debug.Log ("CrashReporter Exception Catched");


				int index = 1;
				LogStruct lastExceptionLog = m_listLogBuffer[m_listLogBuffer.Count-index++];

				while(lastExceptionLog.type == LogType.Log ||
					lastExceptionLog.type == LogType.Warning)
				{
					lastExceptionLog = m_listLogBuffer[m_listLogBuffer.Count-index++];

					if(index >= m_listLogBuffer.Count)
						break;
				}

				switch(m_eWriteMode)
				{
				case eCrashWriteType.EWRITEFILE:
					{
						Routine(SendDebugToFile(lastExceptionLog.type, lastExceptionLog.stacktrace));
					}
					break;
				case eCrashWriteType.EWRITESERVER:
					Routine(SendDebugToServer(lastExceptionLog.type, lastExceptionLog.stacktrace));
					break;
				case eCrashWriteType.EWRITEMAIL:

					Routine(SendDebugToMail(lastExceptionLog.type, lastExceptionLog.stacktrace, m_stServerInfo.GetCurrentSmtp().userID, m_stServerInfo.GetCurrentSmtp().userPwd));
					break;
				case eCrashWriteType.EWRITEGAv3:
					SendDebugToGoogleAnalytics(lastExceptionLog.type, lastExceptionLog.stacktrace);
					break;
				}

				yield return null;
			}
		}
	}

	public void SetUserInfomation(string userid, string useruid, string nickname="", string teamname="")
	{
		m_stUserInfo.userID = userid;
		m_stUserInfo.userUID = useruid;
		m_stUserInfo.nickname = nickname;
		m_stUserInfo.teamname = teamname;
	}

	public void AnalyticsImplement(LogType type, string trace)
	{
//		AnalyticsTracker.Instance.SendEvent("Exception", GetFirstFunctionName(trace),
//			new Dictionary<string, object>{
//				{"trace", trace}
//			});
	}

	void Routine(IEnumerator pRoutine, Action pAction = null)
	{
		m_goMonoBehaviourObject.GetComponent<MonoBehaviour>().StartCoroutine(InvokeToRoutine(pRoutine, pAction));
	}

	IEnumerator InvokeToRoutine(IEnumerator pRoutine, Action pAction)
	{
		yield return m_goMonoBehaviourObject.GetComponent<MonoBehaviour>().StartCoroutine(pRoutine);
		if(pAction!=null)
			pAction.Invoke();
	}

	public void SetCrashReporterOnlineInfo(string strConfigFileHost)
	{
#if !UNITY_EDITOR

		m_bUpdateWaitServerInfo = true;
		Routine(WaitForRequest(new WWW(strConfigFileHost + "?" + Time.time), (msg)=>{
			string json = msg;
			Hashtable table = SG.MiniJsonExtensions.hashtableFromJson(json);

			try {
				m_eWriteMode = (eCrashWriteType)System.Enum.Parse(typeof(eCrashWriteType), table["mode"].ToString());	
				m_strMailingList = table["MailingList"].ToString();

				try {
					m_tCrashReporterLevel = eExceptionType.None;
					ArrayList arr = (ArrayList)table["CrashReporterLevel"];
					foreach (string item in arr) {
						m_tCrashReporterLevel |= (eExceptionType)Enum.Parse(typeof(eExceptionType), item);
					}
				} catch {
					m_tCrashReporterLevel = eExceptionType.Exception;
				}

				if(m_eWriteMode == eCrashWriteType.EWRITEMAIL)
				{
					ArrayList list = (ArrayList)table["GmailList"];
					foreach (Hashtable item in list) {

						m_stServerInfo.SetServerHost("");
						m_stServerInfo.AddSmtp(new SmtpMailInfo(item["id"].ToString(),item["pwd"].ToString(), "smtp.gmail.com", true));
					}

				}else if (m_eWriteMode == eCrashWriteType.EWRITESERVER)
				{
					//					m_stServerInfo.GetCurrentSmtp().userID

					m_stServerInfo.ClearSmtpList();
					m_stServerInfo.ResetSmtpIndex();

					Hashtable hash = (Hashtable)table["ServerInfo"];
					m_stServerInfo.SetServerHost(hash["host"].ToString());

					ArrayList list = (ArrayList)hash["SmtpList"];
					foreach (Hashtable item in list) {
						m_stServerInfo.AddSmtp(new SmtpMailInfo(item["id"].ToString(),item["pwd"].ToString(), item["smtp"].ToString(), (bool)item["ssl"]));
					}
				}

			} catch (Exception ex) {
				m_eWriteMode = eCrashWriteType.EWRITEGAv3;
			}

			m_bUpdateWaitServerInfo = false;
			UnityEngine.Debug.Log ("CrashReporter Change Mode:" + m_eWriteMode.ToString());
		}, ()=>{
			m_eWriteMode = eCrashWriteType.EWRITEGAv3;
			m_bUpdateWaitServerInfo = false;
		}));

#endif
	}
	IEnumerator WaitForRequest(WWW www, Action<string> success, Action fail)
	{
		float timer = 0; 
		bool failed = false;

		while(!www.isDone){
			if(timer > 3){ 
				failed = true; 
				break; 
			}
			timer += Time.deltaTime;
			yield return new WaitForEndOfFrame();
		}

		if (failed || www.error != null) {
			string msg = (failed?"istimeout":www.error);
			Debug.Log(msg);
			fail.Invoke();
		} else {
			success.Invoke(www.text);
		}  
	}

	void WriteFileLog(string message, string trace, bool bException)	
	{
#if UNITY_ANDROID || UNITY_IPHONE
		m_swWriter = new StreamWriter(Path.Combine(Application.persistentDataPath, "unityexceptions.txt"));
#else
		m_swWriter = new StreamWriter(Path.Combine(Application.dataPath, "unityexceptions.txt"));
#endif
		m_swWriter.AutoFlush = true;

		m_swWriter.WriteLine(message);
		m_swWriter.Close();

		if(bException)
		{
#if UNITY_ANDROID || UNITY_IPHONE
			m_swWriter = new StreamWriter(Path.Combine(Application.persistentDataPath, "exceptiontrace.txt"));
#else
			m_swWriter = new StreamWriter(Path.Combine(Application.dataPath, "exceptiontrace.txt"));
#endif
			m_swWriter.AutoFlush = true;

			m_swWriter.WriteLine(message);
			m_swWriter.Close();
		}
	}

	public void SendUnreportedCrashReport()
	{
		Routine(WaitServerInfoRefresh());

	}

	IEnumerator WaitServerInfoRefresh()
	{
		while(m_bUpdateWaitServerInfo==true)	{
			yield return null;
		}
		if(m_eWriteMode == eCrashWriteType.EWRITEMAIL
			|| m_eWriteMode == eCrashWriteType.EWRITESERVER)
		{
			string stackPath = "";
			string bodyPath = "";
#if UNITY_ANDROID || UNITY_IPHONE
			stackPath = Path.Combine(Application.persistentDataPath, "exceptiontrace.txt");
			bodyPath = Path.Combine(Application.persistentDataPath, "unityexceptions.txt");
#else
			stackPath = Path.Combine(Application.dataPath, "exceptiontrace.txt");
			bodyPath = Path.Combine(Application.dataPath, "unityexceptions.txt");
#endif
			if(File.Exists(stackPath) && File.Exists(bodyPath))
			{
				switch(m_eWriteMode)
				{
				case eCrashWriteType.EWRITESERVER:
					Routine(SendDebugToServer(LogType.Exception, File.ReadAllText(stackPath), File.ReadAllText(bodyPath)));
					break;
				case eCrashWriteType.EWRITEMAIL:
					Routine(SendDebugToMail(LogType.Exception, File.ReadAllText(stackPath), m_stServerInfo.GetCurrentSmtp().userID, m_stServerInfo.GetCurrentSmtp().userPwd, File.ReadAllText(bodyPath)));
					break;
				}
			}
		}
	}
	public void FinalWorking(bool bUnreportedCrashWork = false)
	{
		m_listLogBuffer.Clear();
		m_listLogBufferThread.Clear();
		System.GC.Collect();

		if (!Application.isEditor) {
			SomethingReallyBadHappened ();
		}

		string path = "";
#if UNITY_ANDROID || UNITY_IPHONE
		path = Path.Combine(Application.persistentDataPath, "exceptiontrace.txt");
#else
		path = Path.Combine(Application.dataPath, "exceptiontrace.txt");
#endif
		if(File.Exists(path))
			File.Delete(path);

		Debug.Log("FinalWork finish crashreporter");

		if(bUnreportedCrashWork==false && m_tExceptionType == LogType.Exception)
		{
#if UNITY_EDITOR
			UnityEngine.Debug.Log("<color=yellow>Assert&Exception Occured, See the console log</color>");
			// 에디터 콘솔창에 'Error Pause' 토글 버튼이 있기 때문에 강제로 Pause 할 필요는 없다고 판단함[blueasa / 2016-02-19]
#else
			UnityEngine.Debug.Log("Assert&Exception Occured");
			Application.Quit();
#endif
		}

		m_bCrashCatched = false;
		m_tExceptionType = LogType.Log;
	}

}
