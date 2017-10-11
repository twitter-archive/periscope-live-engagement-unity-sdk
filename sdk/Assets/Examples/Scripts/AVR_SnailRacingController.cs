using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AVR_SnailRacingController : MonoBehaviour
{
    [Header("Setup")]
    public List<AVR_Snail> snails;
    public AVR_CameraFollow camera;
    public GameObject ground;
    public Color groundColorDay;
    public Color groundColorNight;
    public Text bottomText;
    public float maxLead = 0.1f;

    [Header("Editor Controls")]
    public bool isTesting = false;
    private bool _isTesting = false;
    public bool isFlip = false;

    [Header("Read Only")]
    public string leader;

	AVR_Snail leadSnail;
	private Renderer groundRenderer;
    private bool isFlipped = false;

	// Use this for initialization
	void Start () {
		groundRenderer = ground.GetComponent<Renderer>();

		StartCoroutine(WaitFlipRepeat());
	}
	
	// Update is called once per frame
	void Update () {
		if (isFlip)
        {
            Flip();
            isFlip = false;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            Flip();
        }

        if (!_isTesting && isTesting)
        {
            // Turn on Testing
            StartTesting();
            _isTesting = isTesting;
        }

        if (_isTesting && !isTesting)
        {
            // Turn off Testing
            StopTesting();
            _isTesting = isTesting;
        }

        // Get Leader
        float leaderX = float.PositiveInfinity; 
        foreach (AVR_Snail snail in snails)
        {
            //Bounds b = item.bodyGeo.GetComponent<Renderer>().bounds;
            //if (b.center.x < leaderX)
            if (snail.bodyGeo.position.x < leaderX)
            {
                leaderX = snail.bodyGeo.position.x;
                leader = snail.name;
                bottomText.text = "Heart to Snail\n" + snail.name + " Leads";
                leadSnail = snail;
                camera.target = leadSnail.bodyGeo.transform;
            }
        }

        // Make sure trailing snails catch up to leader
        foreach (AVR_Snail snail in snails)
        {
            if (snail.bodyGeo.position.x > leadSnail.bodyGeo.position.x + maxLead && !isFlipped)
            {
                snail.MoveFixedAmount((leadSnail.bodyGeo.position.x - snail.bodyGeo.position.x) / 30);
            }
        }
	}

    // FIXME: Calling this too quickly results in bad behavior
    void Flip ()
    {
        // Keep track of state
        isFlipped = !isFlipped;

        // Flip the snails
        foreach (AVR_Snail item in snails)
        {
            item.Flip();
        }

        // Change the environment
        if (isFlipped)
        {
            groundRenderer.material.SetColor("_Color", groundColorNight);
        }
        else
        {
            groundRenderer.material.SetColor("_Color", groundColorDay);
        }
    }

    void StartTesting ()
    {
        foreach (AVR_Snail item in snails)
        {
            StartCoroutine(SnailWaitRepeat(item));
        }
    }

    void StopTesting ()
    {
        StopAllCoroutines();
		StartCoroutine(WaitFlipRepeat());
	}

    IEnumerator SnailWaitRepeat(AVR_Snail snail)
    {
        int moveAmount = (int)Random.Range(1f, 30.0f);
        snail.Move(moveAmount);

        float waitTime = Random.Range(0.2f, 3f);
        yield return new WaitForSecondsRealtime(waitTime);

        StartCoroutine(SnailWaitRepeat(snail));
    }

    IEnumerator WaitFlipRepeat()
    {
        float waitTime = Random.Range(10f, 30f);
        yield return new WaitForSecondsRealtime(waitTime);

        Flip();
        StartCoroutine(WaitFlipRepeat());
    }
}
