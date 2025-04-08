using UnityEngine;

using Wsla.Unity;

public class RoomTimeSinWaveTranslate : MonoBehaviour
{
    [SerializeField]
    float Range = 10;

    [SerializeField]
    float Speed = 2f;

    [SerializeField]
    Vector3 axis = Vector3.right;

    Vector3 Initial;

    NetworkAPI API => NetworkAPI.Instance;
    RoomAPI Room => API.Room;

    void Start()
    {
        Initial = transform.position;
    }

    void Update()
    {
        if (Room.IsConnected is false)
            return;

        var time = Room.Time.CalculateSeconds();

        var offset = Mathf.Sin((float)(time * Speed)) * Range;

        transform.position = Initial + (axis * offset);
    }
}