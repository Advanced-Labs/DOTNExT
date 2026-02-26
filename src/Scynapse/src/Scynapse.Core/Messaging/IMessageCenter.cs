namespace Scynapse.Runtime
{
    internal interface IMessageCenter
    {
        void SendMessage(Message msg);

        void DispatchLocalMessage(Message message);
    }
}
