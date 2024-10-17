using System;

using UnityEngine;

namespace Toolbox
{
    [Serializable]
    public class ButtonInput
    {
        public bool Click { get; protected set; }
        public bool Hold { get; protected set; }
        public bool Lift { get; protected set; }

        public float Duration { get; set; }

        public virtual void Process(bool input)
        {
            if (input)
            {
                Duration += Time.deltaTime;

                Click = !Click && !Hold;

                Lift = false;
            }
            else
            {
                Lift = !Lift && Hold;

                Click = false;

                Duration = 0f;
            }

            Hold = input;
        }
    }
}