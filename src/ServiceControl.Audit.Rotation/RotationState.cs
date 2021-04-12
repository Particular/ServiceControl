// unset

namespace ServiceControl.Audit.Rotation
{
    using System;

    class RotationState
    {
        public int ActiveInstanceIndex { get; set; }
        public DateTime LastRotation { get; set; }
    }
}