using UnityEngine;

namespace Frontier.Runtime
{
    // Presentation only: position samples come from the simulation, never the rendered
    // interpolation lag. Small avoidance corrections should not turn the whole model.
    public sealed class UnitFacing
    {
        public const float TurnDegreesPerSecond = 360;
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        private Quaternion desired = Quaternion.identity;
        private Vector3 previousPosition, filteredVelocity, sampledVelocity, commandDirection;
        private int previousTick;
        private float commandFacingTime, reversalTime;
        private bool reversing;

        public void Reset(Vector3 position, Quaternion rotation, int tick)
        {
            previousPosition = position; previousTick = tick;
            Rotation = desired = rotation; filteredVelocity = sampledVelocity = Vector3.zero;
            commandFacingTime = reversalTime = 0; reversing = false;
        }

        public void FaceDestination(Vector3 position, Vector3 destination)
        {
            var direction = destination - position; direction.y = 0;
            if (direction.sqrMagnitude < .0001f) return;
            desired = Quaternion.LookRotation(direction);
            commandDirection = direction.normalized;
            reversing = Vector3.Dot(sampledVelocity, commandDirection) < -.05f;
            reversalTime = reversing ? .65f : 0;
            filteredVelocity = Vector3.zero;
            // Start the requested turn immediately, before the first movement tick.
            // Then let the actual route take over, including necessary obstacle detours.
            commandFacingTime = .16f;
        }

        public Quaternion Step(Vector3 position, int tick, float delta, bool paused, Vector3? aim = null)
        {
            delta = paused ? 0 : Mathf.Max(0, delta);
            if (tick != previousTick)
            {
                float elapsed = Mathf.Max(1, tick - previousTick) / (float)Core.Simulation.TickRate;
                var velocity = (position - previousPosition) / elapsed; velocity.y = 0;
                filteredVelocity = Vector3.Lerp(filteredVelocity, velocity, 1 - Mathf.Exp(-elapsed / .18f));
                sampledVelocity = velocity;
                // Braking can leave the body moving along its previous heading after a
                // reverse order. Do not turn the model back toward that residual motion.
                if (reversing && Vector3.Dot(velocity, commandDirection) >= -.05f)
                {
                    reversing = false;
                    filteredVelocity = velocity;
                }
                if (commandFacingTime <= 0 && !reversing && velocity.sqrMagnitude > .01f && filteredVelocity.sqrMagnitude > .01f)
                    desired = Quaternion.LookRotation(filteredVelocity);
                previousPosition = position; previousTick = tick;
            }
            if (aim.HasValue && commandFacingTime <= 0 && !reversing)
            {
                var direction = aim.Value; direction.y = 0;
                if (direction.sqrMagnitude > .001f) desired = Quaternion.LookRotation(direction);
            }
            Rotation = Quaternion.RotateTowards(Rotation, desired, TurnDegreesPerSecond * delta);
            commandFacingTime = Mathf.Max(0, commandFacingTime - delta);
            reversalTime = Mathf.Max(0, reversalTime - delta);
            // A route may genuinely start opposite to the clicked destination. After
            // this short grace period, follow that detour instead of aiming forever.
            if (reversalTime <= 0) reversing = false;
            return Rotation;
        }
    }
}
