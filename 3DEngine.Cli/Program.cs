using Engine.Cli;

// The command line an agent uses on this repository: find a running app, ask what it can do,
// drive it, capture what it draws, and stop it. Every answer is one envelope and one exit code.

// Written as UTF-8 on every system, where Windows' console would write the code page it uses, so a
// script reading an answer, as a game's word with an accent, reads the characters the app sent.
Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var (options, rest) = Options.Parse(args);
if (rest.Length == 0) return Help.Print();

return rest[0] switch
{
    "status" => Verbs.Status(options),
    "list" => Verbs.List(options),
    "command" or "cmd" => Verbs.Command(options, rest[1..]),
    "eval" => Verbs.Eval(options, rest[1..]),
    "shot" or "screenshot" => Verbs.Shot(options, rest[1..]),
    "stop" => Verbs.Stop(options),
    "open" => Launch.Open(options, rest[1..]),
    "logs" => Launch.Logs(options, rest[1..]),
    "doctor" => Verbs.Doctor(options),
    "shaders" => Verbs.Shaders(options, rest[1..]),
    "help" or "--help" or "-h" => Help.Print(),
    var verb => Output.Refuse(options, verb, "BAD_ARGUMENT", $"'{verb}' is not a verb. Run 'e3d help' for the list."),
};
