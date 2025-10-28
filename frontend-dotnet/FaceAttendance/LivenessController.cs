using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceAttendance
{
    public enum PoseAction
    {
        LookCenter,
        LookLeft,
        LookRight,
        LookUp,
        LookDown
    }

    public class LivenessController
    {
        public IReadOnlyList<PoseAction> CurrentActions => _actions.AsReadOnly();
        public int CurrentIndex { get; private set; }
        public bool IsPassed => CurrentIndex >= _actions.Count;
        public bool IsActive { get; private set; }

        private List<PoseAction> _actions = new();
        private DateTime _startTime;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(40);

        public void StartNewSession()
        {
            _actions = GenerateRandomActions();
            CurrentIndex = 0;
            _startTime = DateTime.Now;
            IsActive = true;
        }

        public void Reset()
        {
            _actions.Clear();
            CurrentIndex = 0;
            IsActive = false;
        }

        public bool CheckTimeout() => IsActive && DateTime.Now - _startTime > _timeout;

        public void MarkCurrentActionDone()
        {
            CurrentIndex++;
            if (IsPassed) IsActive = false;
        }

        private static List<PoseAction> GenerateRandomActions()
        {
            var rand = new Random();
            var allActions = Enum.GetValues(typeof(PoseAction)).Cast<PoseAction>().ToList();
            var result = new List<PoseAction>();
            const int count = 3;

            for (int i = 0; i < count; i++)
            {
                if (allActions.Count == 0)
                {
                    allActions = Enum.GetValues(typeof(PoseAction)).Cast<PoseAction>().ToList();
                }

                var pick = allActions[rand.Next(allActions.Count)];
                result.Add(pick);
                allActions.Remove(pick);
            }

            if (!result.Contains(PoseAction.LookCenter))
            {
                result[rand.Next(result.Count)] = PoseAction.LookCenter;
            }

            return result;
        }
    }

    public class PoseResult
    {
        public double Yaw { get; set; }
        public double Pitch { get; set; }
        public double Roll { get; set; }
        public double Motion { get; set; }
    }
}