namespace KDG.Zoho.CRM.Enums
{
    public class Triggers
    {
        private const string WORKFLOW = "workflow";
        private const string APPROVAL = "approval";
        private const string BLUEPRINT = "blueprint";
        private const string PATHFINDER = "pathfinder";
        private const string ORCHESTRATION = "orchestration";

        public string Value;
        private Triggers(string value)
        {
            Value = value;
        }
        public static Triggers Workflow { get { return new Triggers(WORKFLOW); } }
        public static Triggers Approval { get { return new Triggers(APPROVAL); } }
        public static Triggers Blueprint { get { return new Triggers(BLUEPRINT); } }
        public static Triggers Pathfinder { get { return new Triggers(PATHFINDER); } }
        public static Triggers Orchestration { get { return new Triggers(ORCHESTRATION); } }
    }
}
