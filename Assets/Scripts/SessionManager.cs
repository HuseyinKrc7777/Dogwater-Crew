using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class SessionManager : MonoBehaviour
{
    [HideInInspector] // UI'dan gelen IP'yi tutacak, Editör'de görmemize gerek yok
    public string serverIP = "127.0.0.1";

    [Header("Ağ Ayarları")]
    public ushort serverPort = 7777;

    private Coroutine _rttCoroutine;

    // Host olma metodu (Senin PC'n)
    public void CreateSession()
    {
        try
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", serverPort); // 0.0.0.0 = "Gelen tüm IP'leri kabul et"

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("<color=green>Host Başlatıldı! Gelen oyuncular bekleniyor...</color>");

                // Oyuna geçiş yap
                NetworkManager.Singleton.SceneManager.LoadScene("ShipTest 1", LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("Host başlatılamadı! Port kullanımda olabilir.");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Client olma metodu (2. ekranın veya Arkadaşların)
    public void JoinSession(string targetIP)
    {
        try
        {
            serverIP = targetIP; // UI'dan gelen IP'yi kaydet

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(serverIP, serverPort); // Hedef IP'ye odaklan

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log($"<color=cyan>{serverIP}</color> adresine bağlanılıyor...");

                // RTT (Gecikme) ölçümünü başlat
                if (_rttCoroutine != null) StopCoroutine(_rttCoroutine);
                _rttCoroutine = StartCoroutine(LogRTTRoutine());
            }
            else
            {
                Debug.LogError("Bağlantı başarısız! Host açık mı veya IP doğru mu?");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Ping / RTT Ölçücü
    private IEnumerator LogRTTRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            if (transport != null)
            {
                ulong rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
                Debug.Log($"<color=yellow>[LAN RTT]</color> Mevcut Gecikme: {rtt} ms");
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private void OnDestroy()
    {
        if (_rttCoroutine != null) StopCoroutine(_rttCoroutine);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}