using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stateful
{
    class Program
    {
        static void Main(string[] args)
        {
            //finalization sample
            var w = CreateWorkflow(null, Process.Finalization, State.New, Role.Owner, State.Finalized);
            w.OnTransitioned((worfklowEvent) => { Console.WriteLine(worfklowEvent); }); //callback \

            //var graph = w.ToGraph();


            var sim = w.Simulate(20);

            Console.ReadLine();

            //change request sample
            var wch = CreateWorkflow(w, Process.ChangeRequest, State.Finalized, Role.Lead, State.Finalized);
            wch.OnTransitioned((worfklowEvent) => { Console.WriteLine(worfklowEvent); });
            sim = wch.Simulate(20);

            Console.ReadLine();

            //cancellation sample
            var wc = CreateWorkflow(w, Process.Cancellation, State.Finalized, Role.Interface, State.Canceled);
            wc.OnTransitioned((worfklowEvent) => { Console.WriteLine(worfklowEvent); });
            sim = wc.Simulate(20);

            Console.ReadLine();

            //closure sample
            var wcl = CreateWorkflow(w, Process.Closeout, State.Finalized, Role.Support, State.Closed);
            wcl.OnTransitioned((worfklowEvent) => { Console.WriteLine(worfklowEvent); });
            sim = wcl.Simulate(20);

            Console.ReadLine();
        }


        public enum Process { Finalization, ChangeRequest, Cancellation, Closeout }
        public enum Role { Lead, Interface, Support, Owner, LeadTC, InterfaceTC, SupportTC, OwnerTC, System }
        public enum State { New, Pending, PendingReview, PendingLeadReview, PendingInterfaceReview, PendingSupportReview, PendingLeadTCReview, PendingInterfaceTCReview, PendingSupportTCReview, Finalized, Canceled, Closed }
        public enum Trigger { SaveDraft, Submit, Approve, Reject, Forward, Return, Cancel, Change }
        public enum Permission { Comment, Upload, Edit }


        public static Workflow<Process, State, Trigger, Role, Permission> CreateWorkflow(Workflow<Process, State, Trigger, Role, Permission> parent, Process process, State initial, Role role, State target)
        {
            var w = new Workflow<Process, State, Trigger, Role, Permission>(process, initial, role);

            w.ParentWorkflow = parent;

            w.AddTransition()
                .AllowInitiatorOnly(Trigger.SaveDraft)
                .From(initial)
                .To(State.Pending)
                ;

            w.AddTransition()
                .AllowInitiatorOnly(Trigger.Submit)
                .From(State.Pending)
                .To((ir) => ir == Role.Lead ? State.PendingInterfaceReview : State.PendingLeadReview)
                ;

            w.AddTransition()
                .Allow(Role.Lead, Trigger.Approve)
                .From(State.PendingLeadReview)
                .To((ir) => ir == Role.Interface ? State.PendingSupportReview : State.PendingInterfaceReview)
                ;

            w.AddTransition()
                .Allow(Role.Lead, Trigger.Forward)
                .From(State.PendingLeadReview)
                .To(State.PendingLeadTCReview)
                ;

            w.AddTransition()
                .Allow(Role.LeadTC, Trigger.Return)
                .From(State.PendingLeadTCReview)
                .To(State.PendingLeadReview)
                ;

            w.AddTransition()
                .Allow(Role.Interface, Trigger.Approve)
                .From(State.PendingInterfaceReview)
                .To((ir) => ir == Role.Support ? State.Finalized : State.PendingSupportReview)
                ;

            w.AddTransition()
                .Allow(Role.Interface, Trigger.Forward)
                .From(State.PendingInterfaceReview)
                .To(State.PendingInterfaceTCReview)
                ;

            w.AddTransition()
                .Allow(Role.InterfaceTC, Trigger.Return)
                .From(State.PendingInterfaceTCReview)
                .To(State.PendingInterfaceReview)
                ;

            w.AddTransition()
                .Allow(Role.Support, Trigger.Approve)
                .From(State.PendingSupportReview)
                .To(target)
                ;

            w.AddTransition()
                .Allow(Role.Support, Trigger.Forward)
                .From(State.PendingSupportReview)
                .To(State.PendingSupportTCReview)
                ;

            w.AddTransition()
                .Allow(Role.SupportTC, Trigger.Return)
                .From(State.PendingSupportTCReview)
                .To(State.PendingSupportReview)
                ;

            //rejection
            w.AddTransition()
                .Allow(Role.Lead, Trigger.Reject)
                .From(State.PendingLeadReview)
                .To(State.Pending)
                ;

            w.AddTransition()
                .Allow(Role.Interface, Trigger.Reject)
                .From(State.PendingInterfaceReview)
                .To(State.Pending)
                ;

            w.AddTransition()
                .Allow(Role.Support, Trigger.Reject)
                .From(State.PendingSupportReview)
                .To(State.Pending)
                ;

            /*
            w.AddTransition() //example for time-based transitions, and can be used for escalations..
                .From(State.Pending)
                .To(State.PendingReview)
                .Allow(Role.System, Trigger.Submit)
                .AutoTriggerAfter(new TimeSpan(5, 0, 0, 0))
                ;

            Func<bool> CheckPackagesAwarded = () => { return true; };
            w.AddTransition() //example for condition-based transitions
                .From(State.Pending)
                .To(State.PendingReview)
                .Allow(Role.System, Trigger.Submit)
                .AutoTriggerWhen(CheckPackagesAwarded)
                ;
            */



            Action<State> SendEmail = (state) => { if (state != State.New && state != State.Pending) Console.WriteLine("\tSend Email to all parties on entering state: " + state.ToString()); };
            w.States.ToList().ForEach(x => { x.OnEntry(SendEmail); x.GrantAll(Permission.Comment); });

            w.ConfigureState(State.Pending).GrantInitiator(Permission.Upload);
            w.ConfigureState(State.PendingLeadReview).Grant(Role.Lead, Permission.Upload);
            w.ConfigureState(State.PendingInterfaceReview).Grant(Role.Interface, Permission.Upload);
            w.ConfigureState(State.PendingSupportReview).Grant(Role.Support, Permission.Upload);

            return w;
        }
    }

    class Workflow<Tw, Ts, Tt, Tr, Tp>
    {
        //TODOs

        //internal transition enum for trigger type: auto, manual, timer
        //transition: allfunc for dynamic role selection
        //custom fields
        //requirements: e.g. a remark is required when reject/return, or an attachment is required..
        //reminder to act
        //inbox
        //impersonation
        //persistence
        //creation
        //deletion

        public enum TransitionType { Forward, Backward, Neutral }

        public Workflow<Tw, Ts, Tt, Tr, Tp> ParentWorkflow { get; set; }

        public Tw Process { get; set; }
        List<Transition> Transitions { get; set; }

        public HashSet<State> States { get; set; }
        public WorkflowState CurrentState { get; set; }
        public Workflow(Tw process, Ts currentState, Tr initiator)
        {
            States = new HashSet<State>();
            foreach (var s in Enum.GetValues(typeof(Ts)))
            {
                States.Add(new State(this, (Ts)s));
            }

            Transitions = new List<Transition>();
            CurrentState = new WorkflowState(currentState, initiator);
        }

        public State ConfigureState(Ts s)
        {
            return States.FirstOrDefault(x => x.InnerState.GetHashCode() == s.GetHashCode());
        }

        public void SetState(Ts newState)
        {
            CurrentState = new WorkflowState(newState, CurrentState.Initiator);
        }

        public void SetInitiator(Tr newInitiator)
        {
            CurrentState = new WorkflowState(CurrentState.State, newInitiator);
        }

        public List<Transition> GetPossibleTransitions()
        {
            var t = Transitions
                .Where(x => x.FromState.GetHashCode() == CurrentState.State.GetHashCode())
                .ToList();
            return t;
        }

        public bool Invoke(Tr tr, Tt tt)
        {
            //find the transitions available for current state
            var t = GetPossibleTransitions()
                .Where(x => x.Trigger.GetHashCode() == tt.GetHashCode())
                .Where(x => x.AllowRoles.Contains(tr))
                .FirstOrDefault();

            if (t != null)
            {
                var newWorkflowState = t.Invoke(tr, tt);

                //invoke state events onexit/onentry
                if (CurrentState.State.GetHashCode() != newWorkflowState.State.GetHashCode())
                {
                    var oldState = States.FirstOrDefault(x => x.InnerState.GetHashCode() == CurrentState.State.GetHashCode());
                    oldState.InvokeOnExit();

                    var newState = States.FirstOrDefault(x => x.InnerState.GetHashCode() == newWorkflowState.State.GetHashCode());
                    newState.InvokeOnEntry();
                }



                var workflowEvent = new WorkflowEvent() { FromState = CurrentState.State, ToState = newWorkflowState.State, Action = tt, PerformedBy = tr, When = DateTime.Now };
                _onTransitioned?.Invoke(workflowEvent);


                CurrentState = newWorkflowState;

                return true;
            }

            return false;
        }

        public Transition AddTransition()
        {
            var t = new Transition(this);
            Transitions.Add(t);
            return t;
        }

        public class RoleTrigger
        {
            public Tr Role { get; set; }
            public Tt Trigger { get; set; }

            public RoleTrigger(Tr tr, Tt tt)
            {
                Role = tr;
                Trigger = tt;
            }
        }

        public class RolePermission
        {
            public Tr Role { get; set; }
            public Tp Permission { get; set; }

            public RolePermission(Tr tr, Tp tp)
            {
                Role = tr;
                Permission = tp;
            }

            public override int GetHashCode()
            {
                return new Tuple<Tr, Tp>(Role, Permission).GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is RolePermission && Equals((RolePermission)obj);
            }

            public bool Equals(RolePermission s)
            {
                return s.GetHashCode() == GetHashCode();
            }
        }

        public class Transition
        {
            public TransitionType TransitionType { get; set; }
            public Workflow<Tw, Ts, Tt, Tr, Tp> Workflow { get; set; }
            public Ts FromState { get; set; }

            public Ts ToState { get; set; }

            public Tt Trigger { get; set; }

            public bool ResetInitiator { get; set; }

            public Func<Tr, Ts> ToStateFunction { get; set; }

            Action<Transition> _onTransitioned;

            public TimeSpan TriggerAfter { get; set; }

            public Func<bool> AutoTriggerFunction { get; set; }


            public HashSet<Tr> AllowRoles { get; set; }

            Func<List<Tr>> AllowRolesFunction { get; set; }

            public Transition Allow(Tt tt, Func<List<Tr>> fn)
            {
                AllowRolesFunction = fn;
                return this;
            }

            public Func<Tr, bool> GuardCondition { get; set; }

            public Transition(Workflow<Tw, Ts, Tt, Tr, Tp> w)
            {
                TransitionType = TransitionType.Forward;
                Workflow = w;
                AllowRoles = new HashSet<Tr>();
            }

            public Transition OfType(TransitionType t)
            {
                TransitionType = t;
                return this;
            }
            public Transition From(Ts from)
            {
                FromState = from;
                return this;
            }

            public Transition To(Ts to)
            {
                ToState = to;
                return this;
            }

            public Transition To(Func<Tr, Ts> stateFinder)
            {
                ToStateFunction = stateFinder;
                return this;
            }

            public Transition Allow(Tr r, Tt t)
            {
                AllowRoles.Add(r);
                Trigger = t;
                return this;
            }

            public Transition Allow(List<Tr> roles, Tt t)
            {
                Trigger = t;
                AllowRoles.Clear();
                foreach (var r in roles)
                {
                    AllowRoles.Add(r);
                }
                return this;
            }
            public Transition AllowAll(Tt t)
            {
                Trigger = t;

                AllowRoles.Clear();
                foreach (var r in Enum.GetValues(typeof(Tr)))
                {
                    AllowRoles.Add((Tr)r);
                }
                return this;
            }

            public Transition AllowInitiatorOnly(Tt t)
            {
                Trigger = t;
                AllowRoles.Clear();
                AllowRoles.Add(Workflow.CurrentState.Initiator);
                return this;
            }

            public Transition Condition(Func<Tr, bool> condition)
            {
                GuardCondition = condition;
                return this;
            }

            public Transition SetAsInitiator(bool reset)
            {
                ResetInitiator = reset;
                return this;
            }

            public Transition OnTransitioned(Action<Transition> action)
            {
                _onTransitioned = action;
                return this;
            }

            public Transition AutoTriggerAfter(TimeSpan time)
            {
                //Trigger = trigger;
                TriggerAfter = time;
                return this;
            }

            public Transition AutoTriggerWhen(Func<bool> condition)
            {
                //Trigger = trigger;
                AutoTriggerFunction = condition;
                return this;
            }

            public Ts GetToState(Tr tr)
            {
                if (ToStateFunction != null)
                {
                    return ToStateFunction(tr);
                }

                return ToState;
            }
            public bool CanInvoke(Tr tr, Tt tt)
            {
                if (AllowRoles.Contains(tr) && Trigger.GetHashCode() == tt.GetHashCode())
                {
                    if (GuardCondition == null)
                    {
                        return true;
                    }
                    else if (GuardCondition != null && GuardCondition(tr))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                return false;
            }

            public WorkflowState Invoke(Tr tr, Tt tt) //decide if this should be here or in the workflow itself..
            {

                if (CanInvoke(tr, tt))
                {
                    var toState = ToState;

                    if (ToStateFunction != null)
                    {
                        toState = ToStateFunction(tr);//todo autofind it when accessing the propery as well?
                    }

                    var newWorkflowState = new WorkflowState(toState, Workflow.CurrentState.Initiator);
                    if (ResetInitiator)
                    {
                        newWorkflowState.Initiator = tr;
                    }

                    if (_onTransitioned != null)
                    {
                        _onTransitioned(this);
                    }

                    return newWorkflowState;
                }

                return null;
            }

            public bool CanAutoTrigger()
            {
                var hasTimeTrigger = TriggerAfter != null;
                var byTimeResult = false;
                var hasConditionTrigger = AutoTriggerFunction != null;
                var byConditionResult = false;

                if (hasTimeTrigger)
                {
                    var state = Workflow.States.FirstOrDefault(x => x.InnerState.GetHashCode() == FromState.GetHashCode());
                    var timeSpan = DateTime.Now.Subtract(state.EntryDate);
                    byTimeResult = timeSpan >= TriggerAfter;
                }


                if (hasConditionTrigger)
                {
                    byConditionResult = AutoTriggerFunction.Invoke();
                }

                if (hasTimeTrigger && hasConditionTrigger)
                    return byTimeResult && byConditionResult; //pass both
                else
                    return (hasTimeTrigger && byTimeResult) || (hasConditionTrigger && byConditionResult);
            }

            public WorkflowState AutoInvoke()
            {
                if (CanAutoTrigger())
                {
                    var toState = ToState;
                    var newWorkflowState = new WorkflowState(toState, Workflow.CurrentState.Initiator);
                    if (ResetInitiator)
                    {
                        newWorkflowState.Initiator = newWorkflowState.Initiator; //auto never changes initiatior i think..
                    }

                    if (_onTransitioned != null)
                    {
                        _onTransitioned(this);
                    }

                    return newWorkflowState;
                }

                return null;
            }

        }

        public class WorkflowState
        {
            public WorkflowState(Ts currentState, Tr initiator)
            {
                State = currentState;
                Initiator = initiator;
            }

            public Tr Initiator { get; set; }
            public Ts State { get; set; }
        }

        public class State
        {
            public Workflow<Tw, Ts, Tt, Tr, Tp> Workflow { get; set; }

            public DateTime EntryDate { get; set; }
            public Ts InnerState { get; set; }
            Action<Ts> _onEntry { get; set; }
            Action<Ts> _onExit { get; set; }
            public HashSet<RolePermission> Permissions { get; private set; }

            public State(Workflow<Tw, Ts, Tt, Tr, Tp> workflow, Ts state)
            {
                Workflow = workflow;
                InnerState = state;
                Permissions = new HashSet<RolePermission>();
            }

            public State Grant(Tr tr, Tp tp)
            {
                var rolePermission = new RolePermission(tr, tp);
                Permissions.Add(rolePermission);

                return this;
            }

            public State GrantAll(Tp tp)
            {
                foreach (var tr in Enum.GetValues(typeof(Tr)))
                {
                    var rolePermission = new RolePermission((Tr)tr, tp);
                    Permissions.Add(rolePermission);
                }

                return this;
            }

            public State GrantInitiator(Tp tp)
            {
                var tr = Workflow.CurrentState.Initiator;
                var rolePermission = new RolePermission(tr, tp);
                Permissions.Add(rolePermission);

                return this;
            }

            public State OnEntry(Action<Ts> onEntry)
            {
                _onEntry = onEntry;
                return this;
            }

            public State OnExit(Action<Ts> onExit)
            {
                _onExit = onExit;
                return this;
            }

            public override int GetHashCode()
            {
                return InnerState.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is State && Equals((State)obj);
            }

            public bool Equals(State s)
            {
                return s.InnerState.GetHashCode() == InnerState.GetHashCode();
            }

            internal void InvokeOnEntry()
            {
                _onEntry?.Invoke(InnerState);
            }

            internal void InvokeOnExit()
            {
                _onExit?.Invoke(InnerState);
            }


        }

        public Graph ToGraph()
        {
            HashSet<Graph.GraphNode> states = new HashSet<Graph.GraphNode>();
            HashSet<Graph.GraphLine> lines = new HashSet<Graph.GraphLine>();

            foreach (var s in States)
            {
                var node = new Graph.GraphNode { Label = s.ToString() };
                states.Add(node);
            }

            foreach (var t in Transitions)
            {
                var from = t.FromState.ToString();

                if (t.ToStateFunction != null)
                {
                    foreach (var role in t.AllowRoles)
                    {
                        var to = t.ToStateFunction(CurrentState.Initiator).ToString();
                        var line = new Graph.GraphLine { From = from, To = to, Label = t.Trigger.ToString() + " by " + role.ToString() };
                        lines.Add(line);
                    }
                }
                else
                {
                    var to = t.ToState.ToString();
                    var rolesStr = string.Join(", ", t.AllowRoles.Select(x => x.ToString()));
                    var label = t.Trigger.ToString();
                    if (t.AutoTriggerFunction != null)
                    {
                        rolesStr = "auto by a system checked condition";
                    }

                    if (t.TriggerAfter != null)
                    {
                        rolesStr += " after " + t.TriggerAfter.TotalDays.ToString() + " days";
                    }

                    var line = new Graph.GraphLine { From = from, To = to, Label = label + " by (" + rolesStr + ")" };
                    lines.Add(line);
                }
            }

            var graph = new Graph { Nodes = states, Lines = lines };

            return graph;
        }

        private Action<WorkflowEvent> _onTransitioned { get; set; }
        public void OnTransitioned(Action<WorkflowEvent> callback)
        {
            _onTransitioned = callback;
        }

        public List<string> Simulate(int maxIterations = 20)
        {
            var i = 0;
            var history = new List<string>();
            var possibleTransitions = GetPossibleTransitions();

            if (possibleTransitions.Count() == 0)
            {
                throw new Exception("Workflow is over, or invalid.");
            }

            var t = possibleTransitions.First();

            HashSet<string> AlreadyTried = new HashSet<string>();

            while (t != null)
            {
                var r = t.AllowRoles.FirstOrDefault();
                if (Invoke(r, t.Trigger))
                {
                    AlreadyTried.Add(r.ToString() + "-" + t.Trigger.ToString());
                    history.Add(string.Format("{0} -> {1} by {2} using {3}", t.FromState, CurrentState.State, r, t.Trigger));
                    i++;
                    if (i > maxIterations)
                    {
                        break;
                    }
                }
                else
                {
                    throw new Exception("Workflow is incomplete");
                }

                try
                {
                    possibleTransitions = GetPossibleTransitions();
                    t = possibleTransitions.Last();
                    r = t.AllowRoles.FirstOrDefault();

                    if (AlreadyTried.Contains(r.ToString() + "-" + t.Trigger.ToString()))
                    {
                        t = possibleTransitions.First();
                    }
                }
                catch
                {
                    t = null;
                }

            }

            return history;
        }

        public class WorkflowEvent
        {
            public Ts FromState { get; set; }
            public Ts ToState { get; set; }
            public Tr PerformedBy { get; set; }
            public Tt Action { get; set; }
            public DateTime When { get; set; }

            public override string ToString()
            {
                return string.Format("{0} -> {1}, {2} by {3}", FromState, ToState, Action, PerformedBy);
            }
        }
    }

    public class Graph
    {
        public HashSet<GraphNode> Nodes { get; set; }
        public HashSet<GraphLine> Lines { get; set; }

        public Graph()
        {
            Nodes = new HashSet<GraphNode>();
            Lines = new HashSet<GraphLine>();
        }

        public class GraphLine
        {
            public string Label { get; set; }
            public string From { get; set; }
            public string To { get; set; }
        }

        public class GraphNode
        {
            public string Label { get; set; }
        }
    }

}
