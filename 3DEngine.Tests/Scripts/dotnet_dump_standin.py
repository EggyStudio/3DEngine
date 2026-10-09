#!/usr/bin/env python3
"""Stands for dotnet-dump in the tests of build/test.py, so they need no dump and no tool.

Asked to analyze a dump with -c COMMAND, it answers as dotnet-dump answers for a test host that
died on a thread the runtime does not run, as a driver's, while its main thread was closing the
device: the thread list with that thread marked, the runtime's threads, no managed exception, no
managed frames on that thread, and the main thread's frames among all of them.
"""

import sys

LOADING = "Loading core dump: testhost-4242.dmp ...\n"

ANSWERS = {
    "threads": LOADING + " 0 0x1A2B (6699)\n*1 0x1A2C (6700)\n 2 0x1A2D (6701)\n",
    "clrthreads": LOADING + """ThreadCount:      2
UnstartedThread:  0
BackgroundThread: 1
PendingThread:    0
DeadThread:       0
Hosted Runtime:   no
                                                                                                            Lock
 DBG   ID     OSID ThreadOBJ           State GC Mode     GC Alloc Context                  Domain           Count Apt Exception
   0    1     1a2b 00005602723C3790    20020 Preemptive  0000000000000000:0000000000000000 000056027237B230 -00001 Ukn
   2    2     1a2d 00005602723C5E80    21220 Preemptive  0000000000000000:0000000000000000 000056027237B230 -00001 Ukn (Finalizer)
""",
    "pe": LOADING + "There is no current managed exception on this thread\n",
    "clrstack -f": LOADING + """OS Thread Id: 0x1a2c (1)
        Child SP               IP Call Site
Failed to start stack walk: 80070057
""",
    "clrstack -all -f": LOADING + """OS Thread Id: 0x1a2b
        Child SP               IP Call Site
00007FFCD482D650 00007F9ED17EB6EB 3DEngine.dll!Engine.GraphicsDevice.DestroyLogicalDevice() + 59 [/src/3DEngine/Graphics/GraphicsDevice.Device.cs @ 204]
00007FFCD48302E0                  [InlinedCallFrame: 00007ffcd48302e0]
00007FFCD48320C0 00007F9ED24719CE 3DEngine.dll!Engine.GraphicsDevice.Dispose() + 46 [/src/3DEngine/Graphics/GraphicsDevice.cs @ 202]
00007FFCD48320E0 00007F9ED24719BF 3DEngine.dll!Engine.Renderer.Dispose() + 31
00007FFCD4832100 00007F9ED24719BF 3DEngine.dll!Engine.App.Shutdown() + 31
OS Thread Id: 0x1a2d
        Child SP               IP Call Site
00007F9ECAFFCD30 00007f9f50cdc312 [DebuggerU2MCatchHandlerFrame: 00007f9ecaffcd30]
""",
}


def main():
    args = sys.argv[1:]
    command = args[args.index("-c") + 1] if "-c" in args else ""
    sys.stdout.write(ANSWERS.get(command, LOADING + f"Unrecognized command '{command}'\n"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
