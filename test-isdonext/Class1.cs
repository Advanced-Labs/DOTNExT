using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace isdonext;


partial interface VPersist 
{

}


public  class User (long ID)
{

    public long ID { get; init; }

    //[Persistent]
    //[Replicated]
    //[Semantic]
    //[Relational]
    //[Versioned]



    //public partial bool SomeProp { get; set; }
    //public partial bool SomeProp { get { return true; } set {  } }
    
    public User() : this(ID: -1)
    {
        this.ID = ID;
    }

    public User(string name) : this()
    {
        this.ID = ID;
    }


    public async Task x()
    {   
        dynamic player = null;
        
        await (player.Age = 20);

    }

}

class Player 
{
    

    public Player()
    {
        //User bob = User.Get( (x) => x.Name == "Bob" );

        User Alice = Get<User>(3209873208);
    }


}

class User_A2FF8CB20 : User
{
    public User_A2FF8CB20(int oid) : base()
    {
    }
}
