using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Wsla;
using Wsla.Unity;

public class Level : NetworkBehaviour
{
    public Button DisconnectButton;

    public static Level Instance { get; private set; }

    void Awake()
    {
        Instance = this;

        DisconnectButton.onClick.AddListener(DisconnectAction);
    }

    public override void Set(NetworkEntity.Behaviour reference)
    {
        base.Set(reference);

        Network.Entity.OnSpawn += SpawnCallback;
    }

    void SpawnCallback()
    {
        SpawnPlayer();
    }

    public void SpawnPlayer()
    {
        Network.Room.Entities.Spawn()
            .SetResource(new NetworkEntityResource(0))
            .SetAuthority(NetworkEntityAuthorityMode.Explicit)
            .WriteTrait("Hello Attribute")
            .SetAuthority(NetworkEntityAuthorityMode.Transferable)
            .Send();
    }

    void DisconnectAction()
    {
        Network.API.Room.Disconnect();
        SceneManager.LoadScene("Main Menu");
    }
}