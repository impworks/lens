using System.Collections.Generic;
using Lens.SyntaxTree.Internals;

namespace Lens.Compiler
{
    /// <summary>
    /// One place a state machine can stop and be resumed at: a state number and the label the
    /// dispatch jumps to for it.
    /// </summary>
    internal class ResumePoint
    {
        public ResumePoint(int state)
        {
            State = state;
            Label = new LabelRef("resume_" + state);
        }

        /// <summary>
        /// The value of the state field while the machine waits here.
        /// </summary>
        public readonly int State;

        /// <summary>
        /// Where execution continues when the machine is resumed.
        /// </summary>
        public readonly LabelRef Label;
    }

    /// <summary>
    /// A protected region of the lowered body, and the states that live inside it.
    ///
    /// This exists because of two IL rules that between them decide the whole shape of the rewrite:
    /// nothing may branch *into* a protected region, and leaving one runs its finally handlers. So
    /// the dispatch cannot be one switch at the top of MoveNext - it has to be a chain, where each
    /// region's own dispatch sits just inside it and the enclosing one only knows how to reach the
    /// region's entry.
    /// </summary>
    internal class LoweredRegion
    {
        public LoweredRegion(LoweredRegion parent, string name, LabelRef unwind)
        {
            Parent = parent;
            Entry = new LabelRef(name);
            Unwind = unwind;
            parent?.Children.Add(this);
        }

        /// <summary>
        /// The region that contains this one, or null for the method body itself.
        /// </summary>
        public readonly LoweredRegion Parent;

        /// <summary>
        /// The label immediately before the region, which is the only way in from outside.
        /// </summary>
        public readonly LabelRef Entry;

        /// <summary>
        /// Where to go to run whatever this region has to run on the way out, when the machine is
        /// being abandoned rather than resumed.
        /// </summary>
        public readonly LabelRef Unwind;

        /// <summary>
        /// The regions directly inside this one.
        /// </summary>
        public readonly List<LoweredRegion> Children = new List<LoweredRegion>();

        /// <summary>
        /// The suspension points that belong to this region rather than to one of its children.
        /// </summary>
        public readonly List<ResumePoint> Points = new List<ResumePoint>();

        /// <summary>
        /// The span of states inside this region, children included.
        ///
        /// A region's body is lowered in one go, so its states are consecutive and the enclosing
        /// dispatch can name the whole region with a single range test.
        /// </summary>
        public int FirstState = int.MaxValue;

        public int LastState = int.MinValue;

        public bool HasStates => LastState >= FirstState;

        /// <summary>
        /// Records that a state belongs to this region and to every region around it.
        /// </summary>
        public void Register(int state)
        {
            for (var curr = this; curr != null; curr = curr.Parent)
            {
                if (state < curr.FirstState)
                    curr.FirstState = state;

                if (state > curr.LastState)
                    curr.LastState = state;
            }
        }
    }
}
