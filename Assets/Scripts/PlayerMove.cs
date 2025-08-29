using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    public float moveSpeed = 5f;
    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    // 오너(본인 클라)만 입력을 읽어서 서버로 전달
    private void Update()
    {
        if (IsOwner && IsClient)
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D, 좌/우
            float v = Input.GetAxisRaw("Vertical");   // W/S, 상/하

            if (h != 0f || v != 0f)
                MoveServerRpc(new Vector2(h, v), Time.deltaTime);
        }
    }

    public override void OnNetworkSpawn()
    {
        // 서버에서 초기 스폰 위치를 간단히 클라이언트ID로 벌려줌(겹침 방지)
        if (IsServer)
        {
            float x = (int)OwnerClientId * 2f;
            transform.position = new Vector3(x, 1f, 0f);
        }
    }

    // 클라 → 서버: 이동 요청
    [ServerRpc]
    private void MoveServerRpc(Vector2 input, float dt)
    {
        Vector3 dir = new Vector3(input.x, 0f, input.y).normalized;
        if (dir.sqrMagnitude > 0f)
        {
            cc.Move(dir * moveSpeed * dt);
            // 바라보는 방향도 서버에서 처리
            transform.forward = Vector3.Slerp(transform.forward, dir, 0.25f);
        }
    }
}
