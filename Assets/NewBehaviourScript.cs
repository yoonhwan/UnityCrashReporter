using UnityEngine;
using System.Collections;

public class NewBehaviourScript : MonoBehaviour {

	public CrashReporter m_cCrashReporter;
	// Use this for initialization
	void Start () {

		Debug.Log("Crash Repoter Sample Start");
		m_cCrashReporter.StartCrashReporter (this.gameObject, 
		                                     projectname: "CrashReporter Test",
		                                     type: eCrashWriteType.EWRITEMAIL,
		                                     clientVersion: "1.0.0",
		                                     gmailID: "",
		                                     gmailPWD: "",
		                                     mailingList: "",
											level: eExceptionType.None);
		m_cCrashReporter.SetCrashReporterOnlineInfo("");
		m_cCrashReporter.SendUnreportedCrashReport();
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
