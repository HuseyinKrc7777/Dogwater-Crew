using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private SessionManager sessionManager;
    [SerializeField] private TextMeshProUGUI sessionCodeText;
    [SerializeField] private TMP_InputField NameInput;


    // Art�k Relay kodu de�il, ba�lan�lacak ki�inin IP adresi girilecek
    [SerializeField] private TMP_InputField joinCodeInput;

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        sessionCodeText.text = "";

        // Kendi pencerende test ederken s�rekli yazmakla u�ra�ma diye varsay�lan Localhost IP'si
        joinCodeInput.text = "127.0.0.1";
    }

    // async ve await kald�r�ld� ��nk� ba�lant� lokalde an�nda kurulur
    private void OnHostClicked()
    {
        SessionManager.LocalPlayerName = NameInput.text;
        sessionManager.CreateSession();

        // Ekrana rastgele harfler yerine, dinlenen IP'yi yazd�r�yoruz
        sessionCodeText.text = "Host A��ld�! IP: " + sessionManager.serverIP;
    }

    private void OnJoinClicked()
    {
        SessionManager.LocalPlayerName = NameInput.text;
        string targetIP = joinCodeInput.text;

        // E�er input bo� b�rak�l�rsa ��kmemesi i�in g�venlik �nlemi
        if (string.IsNullOrEmpty(targetIP))
        {
            targetIP = "127.0.0.1";
        }

        // Input'taki IP'yi SessionManager'a g�nder
        sessionManager.JoinSession(targetIP);
    }
}