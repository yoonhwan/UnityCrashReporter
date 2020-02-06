using UnityEngine;
using System.Collections;

public class CrashTestScene : MonoBehaviour {

	public CrashReporter m_cCrashReporter;
	// Use this for initialization
	void Start () {

		m_cCrashReporter = CrashReporter.StartCrashReporter (this.gameObject, 
		                                     projectname: "CrashReporter Test",
		                                     type: eCrashWriteType.EWRITEMAIL,
		                                     clientVersion: "1.0.0",
		                                     gmailID: "",
		                                     gmailPWD: "",
		                                     mailingList: "",
											 level: eExceptionType.Exception);
		m_cCrashReporter.SetCrashReporterOnlineInfo("");
		m_cCrashReporter.SendUnreportedCrashReport();
		Debug.Log("Crash Repoter Sample Start");
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	void Finish()
	{
		Debug.Log("Crash Repoter Sample Finish");
		m_cCrashReporter.Finish ();
	}

	public void TestRun()
	{
		m_cCrashReporter.m_eWriteMode = eCrashWriteType.EWRITESERVER;
		throw new System.Exception();
	}
}
