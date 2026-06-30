using System.Collections.Generic;
using System.Numerics;
using DiscoAccess.Core.World.Overlays;
using Xunit;

namespace DiscoAccess.Tests
{
    public class CameraFollowTests
    {
        // Records every camera focus point and lock release; the other seam members are unused by CameraFollow.
        private sealed class FakeEnv : IWorldEnvironment
        {
            public readonly List<Vector3> Focused = new List<Vector3>();
            public int Released;
            public Vector3 PlayerPosition => Vector3.Zero;
            public bool HasControl => true;
            public Vector3 TraceMove(Vector3 from, Vector3 intended) => intended;
            public float WallDistance(Vector3 from, Vector3 direction, float range) => range;
            public void FocusCamera(Vector3 point) => Focused.Add(point);
            public void ReleaseCamera() => Released++;
        }

        private const float Step = 2f; // CameraFollow.FocusStepMetres

        [Fact]
        public void FocusesOnce_OnBecomingActive()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(5f, 0f, 5f), active: true);

            Assert.Single(env.Focused);
            Assert.Equal(new Vector3(5f, 0f, 5f), env.Focused[0]);
        }

        [Fact]
        public void DoesNotRefocus_ForSubStepDrift()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(0f, 0f, 0f), active: true);
            cam.Tick(new Vector3(Step - 0.5f, 0f, 0f), active: true); // under the step

            Assert.Single(env.Focused);
        }

        [Fact]
        public void Refocuses_WhenDriftPassesStep()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(0f, 0f, 0f), active: true);
            cam.Tick(new Vector3(Step + 0.1f, 0f, 0f), active: true); // past the step

            Assert.Equal(2, env.Focused.Count);
            Assert.Equal(new Vector3(Step + 0.1f, 0f, 0f), env.Focused[1]);
        }

        [Fact]
        public void DoesNotFocus_WhileInactive()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(5f, 0f, 5f), active: false);

            Assert.Empty(env.Focused);
        }

        [Fact]
        public void ReleasesOnce_WhenGoingInactiveAfterFollowing()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(5f, 0f, 5f), active: true);  // follows (takes the lock)
            cam.Tick(new Vector3(5f, 0f, 5f), active: false); // releases
            cam.Tick(new Vector3(5f, 0f, 5f), active: false); // already released: no second release

            Assert.Equal(1, env.Released);
        }

        [Fact]
        public void DoesNotRelease_IfNeverFollowed()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(5f, 0f, 5f), active: false);
            cam.Release();

            Assert.Equal(0, env.Released);
        }

        [Fact]
        public void RefocusesFresh_AfterReactivating_EvenWhenTargetUnchanged()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);
            var p = new Vector3(5f, 0f, 5f);

            cam.Tick(p, active: true);   // focuses, takes the lock
            cam.Tick(p, active: false);  // releases
            cam.Tick(p, active: true);   // re-focuses fresh despite same point

            Assert.Equal(2, env.Focused.Count);
            Assert.Equal(1, env.Released);
        }

        [Fact]
        public void Release_HandsBackTheCamera_WhenFollowing()
        {
            var env = new FakeEnv();
            var cam = new CameraFollow(env);

            cam.Tick(new Vector3(5f, 0f, 5f), active: true);
            cam.Release();
            cam.Release(); // idempotent

            Assert.Equal(1, env.Released);
        }
    }
}
