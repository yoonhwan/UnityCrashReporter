using UnityEngine;
using System.Collections;

public class NewBehaviourScript : MonoBehaviour {

	public CrashReporter m_cCrashReporter;
	// Use this for initialization
	void Start () {
		m_cCrashReporter.StartCrashReporter (this.gameObject, 
		                                     projectname: "CrashReporter Test",
		                                     type: eCrashWriteType.EWRITEMAIL,
		                                     clientVersion: "1.0.0",//VersionController.GetVersion (),
		                                     gmailID: "queens9tft@gmail.com",
		                                     gmailPWD: "qwerasdf@@",
		                                     mailingList: "yoonhwan.ko@neowiz.com");

	}
	
	// Update is called once per frame
	void Update () {
	
	}

	void Finish()
	{
		m_cCrashReporter.Finish ();
	}

	public void TestRun()
	{
		m_cCrashReporter.m_eWriteMode = eCrashWriteType.EWRITEMAIL;
		throw new System.Exception();
	}
}
