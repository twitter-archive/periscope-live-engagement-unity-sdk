using UnityEngine;
using System.Collections;

public class AVR_Bar : AVR_HeartHandler 
{
	public KeyCode moveKey;

    public bool isMove = false;
	public float delta = 0.01f;

	[Header("Bar Progress")]
	public float progress = 0.5f;

	private Renderer r;

    bool increasing;

	// Use this for initialization
    void Start()
    {
		r = GetComponent<Renderer> ();
        StartCoroutine(WaitToggleRepeat());
	}
	
	// Update is called once per frame
    void Update()
    {
		if (isMove || Input.GetKeyUp(moveKey))
		{
			isMove = false;
            Move();
		}
    }

    public void Move()
    {
        if (increasing)
        {
            Increase();
        }
        else
        {
            Decrease();
        }
    }

    void Increase()
    {
        progress = Mathf.Clamp(progress + delta, 0f, 1f);
        r.material.SetFloat("_Balance", 1 - progress);
    }

    void Decrease()
    {
        progress = Mathf.Clamp(progress - delta, 0f, 1f);
        r.material.SetFloat("_Balance", 1 - progress);
    }

    void Toggle()
    {
        increasing = !increasing;
    }

    public override void HandleHearts(int n)
    {
        if (increasing)
        {
            for (int i = 0; i < n; i++)
            {
                Increase();
            }
        }
        else
        {
			for (int i = 0; i < n; i++)
			{
                Decrease();
			}
        }
    }

	IEnumerator WaitToggleRepeat()
	{
		yield return new WaitForSeconds(3f);

		Toggle();

        StartCoroutine(WaitToggleRepeat());
	}
}
