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

    // ����(���� Ŭ��)�� �Է��� �о ������ ����
    private void Update()
    {
        if (IsOwner && IsClient)
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D, ��/��
            float v = Input.GetAxisRaw("Vertical");   // W/S, ��/��

            if (h != 0f || v != 0f)
                MoveServerRpc(new Vector2(h, v), Time.deltaTime);
        }
    }

    public override void OnNetworkSpawn()
    {
        // �������� �ʱ� ���� ��ġ�� ������ Ŭ���̾�ƮID�� ������(��ħ ����)
        if (IsServer)
        {
            float x = (int)OwnerClientId * 2f;
            transform.position = new Vector3(x, 1f, 0f);
        }
    }

    // Ŭ�� �� ����: �̵� ��û
    [ServerRpc]
    private void MoveServerRpc(Vector2 input, float dt)
    {
        Vector3 dir = new Vector3(input.x, 0f, input.y).normalized;
        if (dir.sqrMagnitude > 0f)
        {
            cc.Move(dir * moveSpeed * dt);
            // �ٶ󺸴� ���⵵ �������� ó��
            transform.forward = Vector3.Slerp(transform.forward, dir, 0.25f);
        }
    }
}
