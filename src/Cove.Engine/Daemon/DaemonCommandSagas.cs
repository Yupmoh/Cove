namespace Cove.Engine.Daemon;

internal sealed class DaemonCommandSagas
{
    private Cove.Tasks.Dispatch.DispatchSaga? _dispatch;
    private Cove.Tasks.Dispatch.ResumeSaga? _resume;

    public DaemonCommandSagas(
        Cove.Tasks.Dispatch.DispatchSaga? dispatch,
        Cove.Tasks.Dispatch.ResumeSaga? resume)
    {
        Set(dispatch, resume);
    }

    public Cove.Tasks.Dispatch.DispatchSaga? Dispatch => Volatile.Read(ref _dispatch);

    public Cove.Tasks.Dispatch.ResumeSaga? Resume => Volatile.Read(ref _resume);

    public void Set(
        Cove.Tasks.Dispatch.DispatchSaga? dispatch,
        Cove.Tasks.Dispatch.ResumeSaga? resume)
    {
        Volatile.Write(ref _dispatch, dispatch);
        Volatile.Write(ref _resume, resume);
    }
}
