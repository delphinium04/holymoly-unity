using UnityEngine;
using Unity.Netcode;

public class FollowOwnerCamera : NetworkBehaviour
{
    public Vector3 offset = new Vector3(0, 8, -8);

    public override void OnNetworkSpawn()
    {
        if (IsOwner && Camera.main != null)
        {
            var cam = Camera.main.transform;
            cam.SetParent(transform);
            cam.localPosition = offset;
            cam.localRotation = Quaternion.Euler(35, 0, 0);
        }
    }
}
