# Proliferate

Features:
* Uses Reflection.Emit to dynamically generate an executable file for launching the child process (don't worry this only takes ~20ms).
* Using raw Named Pipes means faster startup time than using WCF (Windows Communication Foundation).
* No dependency on System.ServiceModel.
* Child process shuts down when signaled to do so by parent or when it stops receiving pings.
* Provides ability to directly write to/read from a stream or to send strings.
* Supports multiple simultaneous connections.