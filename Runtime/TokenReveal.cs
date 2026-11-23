using Photon.Pun;
using UnityEngine;

namespace FortnitePirateToken.Runtime;

/// <summary>
/// On the very first touch of a token, plays the "You found Pirate Shop Access" animation and sound on the screen of
/// the player who touched it. Everyone else is told the token has been found so it never plays twice.
/// </summary>
internal sealed class TokenReveal : MonoBehaviourPun
{
    private PhysGrabObject? _grabObject;
    private bool _found;

    private void Awake()
    {
        _grabObject = GetComponent<PhysGrabObject>();
    }

    private void Update()
    {
        // grabbedLocal is only true while the local player is holding it.
        if (_found || _grabObject == null || !_grabObject.grabbedLocal) return;

        _found = true;

        if (SemiFunc.IsMultiplayer() && photonView != null && photonView.ViewID != 0)
        {
            photonView.RPC(nameof(TokenFoundRPC), RpcTarget.OthersBuffered);
        }

        FoundAnimation.Play();
    }

    [PunRPC]
    private void TokenFoundRPC()
    {
        _found = true;
    }
}
