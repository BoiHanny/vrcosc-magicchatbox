using System;
using System.Threading.Tasks;

namespace vrcosc_magicchatbox.Core.Toast;

public sealed record ToastAction(string Label, Func<Task> Execute);
