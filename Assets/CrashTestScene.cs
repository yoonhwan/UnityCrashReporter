using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System.Threading;

public class CrashTestScene : MonoBehaviour {

	public CrashReporter m_cCrashReporter;
	// Use this for initialization
	void Start () {

		m_cCrashReporter = CrashReporter.StartCrashReporter (this.gameObject, 
		                                     Application.productName,
		                                     type: eCrashWriteType.EWRITEFILE,
		                                     Application.version,
		                                     gmailID: "",
		                                     gmailPWD: "",
		                                     mailingList: "yoonhwan.ko@neowiz.com;test@neowiz.com",
											 level: eExceptionType.Exception | eExceptionType.Assert);
		
		m_cCrashReporter.SetIgnoreSubThreadExceptions(typeof(SemaphoreFullException), typeof(SocketException));

		m_cCrashReporter.SetCrashReporterOnlineInfo("http://msg.devmdl.pmang.com/Temp/bd/ConfigClient/crashreporter.info");
		m_cCrashReporter.SendUnreportedCrashReport();
		Debug.Log("Crash Repoter Sample Start");
	}
	
	void Finish()
	{
		Debug.Log("Crash Repoter Sample Finish");
		m_cCrashReporter.Finish ();
	}

	public void TestRun()
	{
		m_cCrashReporter.TestRun();
	}
}
