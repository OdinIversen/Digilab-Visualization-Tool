using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CustomAPI : SensorAPIBase
{
    private const string ClientId = "Tour client Id here";
    private const string ClientSecret = "Your client secret here";
    private const string TokenUrl = "https://api2.arduino.cc/iot/v1/clients/token";
    private const string ApiUrl = "https://api2.arduino.cc/iot/v2/devices";

    [System.Serializable]
    public class Property
    {
        public string variable_name;
        public string last_value;
        public object GetValueAsCorrectType()
        {
            if (bool.TryParse(last_value, out bool boolValue))
                return boolValue;

            if (float.TryParse(last_value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                return floatValue;

            return last_value;  // Return as string if neither bool nor float
        }
        public override string ToString()
        {
            return $"Variable Name: {variable_name}, Last Value: {last_value}";
        }
    }

    [System.Serializable]
    public class Thing
    {
        public string name;
        public List<Property> properties;
        public int properties_count;
    }

    [System.Serializable]
    public class Device
    {
        public Thing thing;
    }

    [System.Serializable]
    public class DeviceWrapper
    {
        public List<Device> devices;
    }

    [SerializeField] public List<Device> devices;

    protected override void OnStart()
    {
        GetData();
    }

    private void GetData()
    {
        StartCoroutine(GetDataCoroutine());
    }

    private IEnumerator GetDataCoroutine()
    {
        string token = null;

        yield return StartCoroutine(GetTokenAsync((accessToken) => { token = accessToken; }));

        using (UnityWebRequest request = UnityWebRequest.Get(ApiUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error: {request.error}");
            }
            else
            {
                string responseData = request.downloadHandler.text;
                DeviceWrapper deviceWrapper = JsonUtility.FromJson<DeviceWrapper>("{\"devices\":" + responseData + "}");
                devices = deviceWrapper.devices;
            }
        }
    }

    private IEnumerator GetTokenAsync(Action<string> callback)
    {
        WWWForm form = new WWWForm();
        form.AddField("client_id", ClientId);
        form.AddField("client_secret", ClientSecret);
        form.AddField("audience", "https://api2.arduino.cc/iot");
        form.AddField("grant_type", "client_credentials");

        using (UnityWebRequest request = UnityWebRequest.Post(TokenUrl, form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error: {request.error}");
            }
            else
            {
                TokenResponse tokenResponse = JsonUtility.FromJson<TokenResponse>(request.downloadHandler.text);
                callback?.Invoke(tokenResponse.access_token);
            }
        }
    }

    [Serializable]
    private class TokenResponse
    {
        public string access_token;
        public int expires_in;
    }



    public override IEnumerator GetSensorDataCoroutine(Action<List<Sensor>> callback)
    {
        yield return StartCoroutine(GetDataCoroutine());
        callback?.Invoke(ConvertDevicesToSensorData());
    }
    private List<Sensor> ConvertDevicesToSensorData()
    {
        List<Sensor> sensorData = new List<Sensor>();

        foreach (var device in devices)
        {
            Sensor sensor = new Sensor();
            sensor.name = device.thing.name;
            sensor.data = new List<StringObjectPair>();

            foreach (var property in device.thing.properties)
            {
                StringObjectPair pair = new StringObjectPair();
                pair.Key = property.variable_name;
                pair.Value = property.GetValueAsCorrectType();
                sensor.data.Add(pair);
            }

            sensorData.Add(sensor);
        }

        return sensorData;
    }
}