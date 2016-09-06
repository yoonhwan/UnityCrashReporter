using UnityEngine;
using System.Collections;

public class NewBehaviourScript : MonoBehaviour {

	public CrashReporter m_cCrashReporter;
	// Use this for initialization
	void Start () {
		m_cCrashReporter.StartCrashReporter (this.gameObject, 
		                                     projectname: "CrashReporter Test",
		                                     type: eCrashWriteType.EWRITEMAIL,
		                                     clientVersion: "1.0.0",
		                                     gmailID: "",
		                                     gmailPWD: "",
		                                     mailingList: "yoonhwan.ko@neowiz.com;yoonhwan.ko@gmail.com");
		m_cCrashReporter.SetCrashReporterOnlineInfo("http://msg.devmdl.pmang.com/ConfigClient/crashreporter.info");
	}
	
	// Update is called once per frame
	int testcounter = 0;
	void Update () {
		Debug.Log(testcounter++);
	}

	void Finish()
	{
		m_cCrashReporter.Finish ();
	}

	public void TestRun()
	{
		m_cCrashReporter.m_eWriteMode = eCrashWriteType.EWRITESERVER;
		throw new System.Exception();
	}
}
