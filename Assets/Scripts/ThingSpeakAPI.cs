using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Globalization;
using System;

public class ThingSpeakAPI : SensorAPIBase
{
    private readonly List<string> channelIds = new List<string> { "2015606", "2109990", "2110036", "2014495" };
    private const string baseUrl = "https://api.thingspeak.com/channels/{0}/feeds.json?&results=1";

    [System.Serializable]
    public class Feed
    {
        public string entry_id;
        public string field1;
        public string field2;
        public string field3;
        public string field4;
        public string field5;
    }

    [System.Serializable]
    public class Channel
    {   
        public string name;
        public string field1;
        public string field2;
        public string field3;
        public string field4;
        public string field5;
    }

    [System.Serializable]
    public class ThingSpeakData
    {
        public Channel channel;
        public List<Feed> feeds;
    }

    [SerializeField] private ThingSpeakData data;

     public override IEnumerator GetSensorDataCoroutine(Action<List<Sensor>> callback)
    {
        List<Sensor> sensors = new List<Sensor>();

        foreach (var channelId in channelIds)
        {
            string apiUrl = string.Format(baseUrl, channelId);
            using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Error: {request.error}");
                }
                else
                {
                    string responseData = request.downloadHandler.text;
                    ThingSpeakData data = JsonUtility.FromJson<ThingSpeakData>(responseData);
                    ProcessThingSpeakData(data, sensors);
                }
            }
        }

        callback?.Invoke(sensors);
    }

    private void ProcessThingSpeakData(ThingSpeakData data, List<Sensor> sensors)
    {
        foreach (var feed in data.feeds)
        {
            Sensor sensor = new Sensor
            {
                name = data.channel.name,
                data = new List<StringObjectPair>
                {
                    new StringObjectPair { Key = data.channel.field1, Value = TryParse(feed.field1) },
                    new StringObjectPair { Key = data.channel.field2, Value = TryParse(feed.field2) },
                    new StringObjectPair { Key = data.channel.field3, Value = TryParse(feed.field3) },
                    new StringObjectPair { Key = data.channel.field4, Value = TryParse(feed.field4) },
                    new StringObjectPair { Key = data.channel.field5, Value = TryParse(feed.field5) },
                }
            };

            sensors.Add(sensor);
        }
    }

    private float TryParse(string value)
    {
        float result;
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        return result;
    }
}