using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AVR_Snail : AVR_HeartHandler
{
    public Transform bodyGeo;
    public Transform shellGeo;

    public float velocityBoost = -0.05f;
    public float velocityDecay = 0.9f;

    public AnimationCurve flipAnimationCurve;

    public KeyCode moveKey;

    [Header("Editor Controls")]
    public bool isMove = false;

    public bool isFlip = false;

    private float velocity = 0f;
    private bool protectAdditionalVelocity = false;

	void Update () {

        if (isMove || Input.GetKeyUp(moveKey))
        {
            isMove = false;
            Move();
        }

        if (isFlip)
        {
            isFlip = false;
            Flip();
        }

        // Move in X
        Vector3 p = bodyGeo.transform.position;
        bodyGeo.transform.position = new Vector3(p.x + velocity, p.y, p.z);

        velocity *= velocityDecay;
    }

    public void Move(int i = 1)
    {
        if (!protectAdditionalVelocity)
        {
            velocity += (i * velocityBoost);
        }
    }

    public void MoveFixedAmount(float x)
    {
        protectAdditionalVelocity = true;
        velocity = 0.0f;
		Vector3 p = bodyGeo.transform.position;
		bodyGeo.transform.position = new Vector3(p.x + x, p.y, p.z);
        protectAdditionalVelocity = false;
    }

    public override void HandleHearts(int n)
    {
        Move(n);
    }

    public void Flip()
    {
        velocity = 0.0f;
        protectAdditionalVelocity = true;
        float rotation = bodyGeo.transform.localEulerAngles.y;
        StartCoroutine(Animate(fromValue: 0f, toValue: 180f, duration: 1f, animationCurve: flipAnimationCurve, operation: x =>
        {
            bodyGeo.transform.localEulerAngles = new Vector3(0f, rotation + x, 0f);
        }, 
        done: () =>
        {
            velocityBoost *= -1f;
            shellGeo.GetComponent<Rigidbody>().isKinematic = false;
			protectAdditionalVelocity = false;
		}));
    }

    IEnumerator Animate(float fromValue, float toValue, float duration, AnimationCurve animationCurve, System.Action<float> operation, System.Action done)
    {
        yield return new WaitUntil(() => Mathf.Abs(velocity) <= 0.001f);
        shellGeo.GetComponent<Rigidbody>().isKinematic = true;

        float elapsedTime = 0.0f;
        while (elapsedTime < duration)
        {
            yield return new WaitForEndOfFrame();
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float tt = animationCurve.Evaluate(t);
            float value = Mathf.Lerp(fromValue, toValue, tt);
            operation(value);
        }
        done();
    }
}
