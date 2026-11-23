using Photon.Pun;
using UnityEngine;

namespace FortnitePirateToken.Runtime;

/// <summary>
/// The first time anyone touches a token, the "You found Pirate Shop Access" animation and sound play on every
/// player's screen. After that the token is "found": touching it again, by anyone, plays nothing.
///
/// In multiplayer the host decides who was first, so two players grabbing at the same moment cannot both trigger it.
/// </summary>
internal sealed class TokenReveal : MonoBehaviourPun
{
    private PhysGrabObject? _grabObject;

    // True once this token has been found. The host's copy is the one that counts; the animation message tells
    // everyone else too (so a new host also knows).
    private bool _found;

    // This player already told the host about their grab, so it is not sent again every frame.
    private bool _reported;

    private void Awake()
    {
        _grabObject = GetComponent<PhysGrabObject>();
    }

    private void Update()
    {
        // grabbedLocal is only true while the local player is holding it.
        if (_reported || _grabObject == null || !_grabObject.grabbedLocal) return;

        _reported = true;

        if (!SemiFunc.IsMultiplayer() || photonView == null || photonView.ViewID == 0)
        {
            Found();
            return;
        }

        photonView.RPC(nameof(RequestFoundRPC), RpcTarget.MasterClient);
    }

    private void Found()
    {
        if (_found) return;

        _found = true;
        FoundAnimation.Play();
    }

    // Sent to the host by whoever grabbed the token first (from their point of view).
    [PunRPC]
    private void RequestFoundRPC()
    {
        if (_found) return;

        // The host's own copy is marked by the same message every player gets.
        photonView.RPC(nameof(PlayFoundRPC), RpcTarget.All);
    }

    // Sent by the host to everyone, once per token.
    [PunRPC]
    private void PlayFoundRPC()
    {
        Found();
    }
}
