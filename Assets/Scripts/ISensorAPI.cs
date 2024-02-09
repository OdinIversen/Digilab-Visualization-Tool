using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;


public interface ISensorAPI
{
    IEnumerator GetSensorDataCoroutine(Action<List<Sensor>> callback);
}

[System.Serializable]
public class Sensor
{
    public string name;
    public List<StringObjectPair> data;
}


[System.Serializable]
public class StringObjectPair
{
    public string Key;
    public object Value;
}

public abstract class SensorAPIBase : MonoBehaviour, ISensorAPI
{
    public float IntervalInSeconds = 1f;

    private IEnumerator coroutine;
    public event Action<List<Sensor>> OnSensorDataUpdated;


    protected void Start()
    {
        coroutine = RepeatGetSensorData(IntervalInSeconds);
        StartCoroutine(coroutine);
        OnStart();
    }

    protected void RaiseOnSensorDataUpdated(List<Sensor> sensors)
    {
        OnSensorDataUpdated?.Invoke(sensors);
    }

    protected virtual void OnStart()
    {
        // This method can be overridden by API implementations to add their own behavior at start
    }

    // Abstract method to be implemented by subclasses
    public abstract IEnumerator GetSensorDataCoroutine(Action<List<Sensor>> callback);

    private IEnumerator RepeatGetSensorData(float interval)
    {
        List<Sensor> currentSensorData = null;

        while (true)
        {
            yield return GetSensorDataCoroutine(data => currentSensorData = data);
            yield return new WaitForSeconds(interval);
        }
    }

    public void SetInterval(float newInterval)
    {
        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        IntervalInSeconds = newInterval;
        coroutine = RepeatGetSensorData(IntervalInSeconds);
        StartCoroutine(coroutine);
    }
}
