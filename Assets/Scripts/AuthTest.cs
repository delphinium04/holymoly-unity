using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class AuthTest : MonoBehaviour
{
    async void Start()
    {
        // UGS 초기화
        await UnityServices.InitializeAsync();

        // 익명 로그인
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log($"✅ 로그인 성공! PlayerID: {AuthenticationService.Instance.PlayerId}");
    }
}
