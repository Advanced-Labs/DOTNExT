using System.Xml.Linq;

namespace TestGrains
{
    /// <summary>
    /// all chat events implement this interface, to define how each event changes the XML document
    /// </summary>
    public interface IChatEvent
    {
        void Update(XDocument document);
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class CreatedEvent : IChatEvent
    {
        [Scynapse.Id(0)]
        public DateTime Timestamp { get; set; }
        [Scynapse.Id(1)]
        public string Origin { get; set; }

        public void Update(XDocument document)
        {
            document.Initialize(Timestamp, Origin);
        }
    }


    [Serializable]
    [Scynapse.GenerateSerializer]
    public class PostedEvent : IChatEvent
    {
        [Scynapse.Id(0)]
        public Guid Guid { get; set; }
        [Scynapse.Id(1)]
        public string User { get; set; }
        [Scynapse.Id(2)]
        public DateTime Timestamp { get; set; }
        [Scynapse.Id(3)]
        public string Text { get; set; }

        public void Update(XDocument document)
        {
            var container = document.GetPostsContainer();
            container.Add(ChatFormat.MakePost(Guid, User, Timestamp, Text));
            document.EnforceLimit();
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class DeletedEvent : IChatEvent
    {
        [Scynapse.Id(0)]
        public Guid Guid { get; set; }

        public void Update(XDocument document)
        {
            document.FindPost(Guid.ToString())?.Remove();
        }
    }

    [Serializable]
    [Scynapse.GenerateSerializer]
    public class EditedEvent : IChatEvent
    {
        [Scynapse.Id(0)]
        public Guid Guid { get; set; }
        [Scynapse.Id(1)]
        public string Text { get; set; }

        public void Update(XDocument document)
        {
            document.FindPost(Guid.ToString())?.ReplaceText(Text);
        }
    }
}
   