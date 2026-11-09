using UnityEngine;

namespace VRClass.Interaction
{
    /// <summary>
    /// 部品の元の状態（位置・回転・大きさ・付いていたソケット）。再配置・強調の前に保存し、終わったら（一時停止でもすぐに）戻す。
    /// SPEC 4.2：再配置した部品は、強調の④復帰で再配置する前の元の位置へ戻す。
    /// </summary>
    public sealed class PartPose
    {
        private PartPose(PartInteraction part)
        {
            Part = part;
            Position = part.transform.position;
            Rotation = part.transform.rotation;
            LocalScale = part.transform.localScale;
            Socket = part.CurrentSocket;
        }

        public PartInteraction Part { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 LocalScale { get; }

        /// <summary>保存したときに付いていたソケット。なければ null。</summary>
        public PartSocket Socket { get; }

        /// <summary>プログラムで動かすために、ソケットから外して空中に止めているか。</summary>
        public bool IsDetached { get; private set; }

        public static PartPose Capture(PartInteraction part)
        {
            return part != null ? new PartPose(part) : null;
        }

        /// <summary>プログラムで動かせるよう、ソケットから外し（記録なし）、空中に止める。何度呼んでもよい。</summary>
        public void DetachForMove()
        {
            if (IsDetached)
            {
                return;
            }

            if (Socket != null)
            {
                Socket.Release(Part);
            }

            Part.SetFloating(true);
            IsDetached = true;
        }

        /// <summary>保存した状態へすぐ戻す。ソケットに付いていたなら付け直す（socket_ok は記録しない）。</summary>
        public void Restore()
        {
            if (Part == null)
            {
                return;
            }

            Part.transform.localScale = LocalScale;
            Part.transform.SetPositionAndRotation(Position, Rotation);
            var rb = Part.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = Position;
                rb.rotation = Rotation;
            }

            Part.SetFloating(false);
            if (Socket != null && Part.CurrentSocket != Socket)
            {
                Socket.ForcePlace(Part, logSocketOk: false);
            }

            IsDetached = false;
        }
    }
}
