using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private SessionManager sessionManager;
    [SerializeField] private TextMeshProUGUI sessionCodeText;

    // Artýk Relay kodu deðil, baðlanýlacak kiþinin IP adresi girilecek
    [SerializeField] private TMP_InputField joinCodeInput;

    private void Awake()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        sessionCodeText.text = "";

        // Kendi pencerende test ederken sürekli yazmakla uðraþma diye varsayýlan Localhost IP'si
        joinCodeInput.text = "127.0.0.1";
    }

    // async ve await kaldýrýldý çünkü baðlantý lokalde anýnda kurulur
    private void OnHostClicked()
    {
        sessionManager.CreateSession();

        // Ekrana rastgele harfler yerine, dinlenen IP'yi yazdýrýyoruz
        sessionCodeText.text = "Host Açýldý! IP: " + sessionManager.serverIP;
    }

    private void OnJoinClicked()
    {
        string targetIP = joinCodeInput.text;

        // Eðer input boþ býrakýlýrsa çökmemesi için güvenlik önlemi
        if (string.IsNullOrEmpty(targetIP))
        {
            targetIP = "127.0.0.1";
        }

        // Input'taki IP'yi SessionManager'a gönder
        sessionManager.JoinSession(targetIP);
    }
}