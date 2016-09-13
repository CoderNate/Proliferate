# Proliferate

Features:
* Uses Reflection.Emit to dynamically generate a tiny executable file for launching a child process (don't worry this only takes ~20ms).
* Using raw Named Pipes means a little bit faster startup time than using WCF (Windows Communication Foundation).
* No dependency on System.ServiceModel.
* Child process shuts down when signaled to do so by parent or when it stops receiving pings.
* Provides ability to directly write to/read from a stream or to send strings.
* Allows for sending serializable objects (using BinaryFormatter internally).
* Supports multiple simultaneous connections.