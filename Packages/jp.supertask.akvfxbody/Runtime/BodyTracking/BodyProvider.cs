using System;
using System.Threading.Tasks;

public class BodyProvider : BackgroundDataProvider
{
    public void StartClientThread(int id) { }
    protected override void RunBackgroundThreadAsync(int id) { }
    public void StopClientThread() { }
}
