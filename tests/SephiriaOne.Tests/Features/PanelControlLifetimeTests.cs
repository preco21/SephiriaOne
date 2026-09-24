using SephiriaOne;

internal static class PanelControlLifetimeTests
{
    private sealed class Control
    {
        // Membership must not use an overloaded/native equality implementation.
        public override bool Equals(object? other) => other is Control;
        public override int GetHashCode() => 0;
    }

    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        void Fails(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { checks++; return; }
            throw new Exception("Expected the injected native callback failure");
        }
        var target = new Control();
        var foreign = new Control();
        var stack = new List<List<Control>> { new() { foreign } };
        var lifetime = new PanelControlLifetime<Control>(target, () => stack);
        bool visible = false;
        int removals = 0, counter = 0;
        void Deactivate() => visible = false;
        void Add() { visible = true; stack.Add(new List<Control> { target }); counter++; }
        void Remove()
        {
            removals++;
            stack.RemoveAll(group => group.Any(item => ReferenceEquals(item, target)));
            counter--;
        }

        Check(!lifetime.IsRegistered, "Foreign controls with equal values are not our registration");
        Fails(() => lifetime.Open(() => throw new InvalidOperationException("before registration")));
        lifetime.Close(Remove, Deactivate);
        Check(removals == 0 && counter == 0, "Failed opening before insertion must not decrement native counters");

        Fails(() => lifetime.Open(() => { Add(); throw new InvalidOperationException("after registration"); }));
        Check(lifetime.IsRegistered, "A callback failure after insertion retains a real registration");
        Fails(() => lifetime.Open(Add));
        Check(counter == 1 && stack.Count == 2, "Pending registration cannot be opened twice");
        lifetime.Close(Remove, Deactivate);
        Check(!lifetime.IsRegistered && !visible && counter == 0 && removals == 1,
            "Disposal cleans partial registration and deactivates its panel");
        lifetime.Close(Remove, Deactivate);
        Check(removals == 1 && counter == 0, "Repeated close cannot decrement the native counter twice");

        lifetime.Open(Add);
        Fails(() => lifetime.Close(() => throw new InvalidOperationException("before removal"), Deactivate));
        Check(lifetime.IsRegistered && !visible && counter == 1,
            "Failure before removal retains a hidden object for a later cleanup attempt");
        lifetime.Close(Remove, Deactivate);
        Check(!lifetime.IsRegistered && counter == 0 && removals == 2, "A later cleanup attempt removes the retained entry");

        lifetime.Open(Add);
        Fails(() => lifetime.Close(() => { Remove(); throw new InvalidOperationException("after removal"); }, Deactivate));
        Check(!lifetime.IsRegistered && !visible && counter == 0, "Post-removal failure still permits safe disposal");
        lifetime.Close(Remove, Deactivate);
        Check(removals == 3 && counter == 0, "Post-removal failure must not cause duplicate native removal");

        lifetime.Open(Add);
        stack.Clear(); // The retained native manager/root has ended its lifetime.
        lifetime.Close(Remove, Deactivate);
        Check(!visible && !lifetime.IsRegistered && removals == 3, "A vanished native scope needs no removal callback");
        return checks;
    }
}
