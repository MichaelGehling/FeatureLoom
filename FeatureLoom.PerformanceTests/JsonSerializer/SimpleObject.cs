using System.Collections.Generic;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

public class SimpleObject
{
    public int id = 0;
    public string name = "This is a string";
    public double value = 123.456;    
}

public class UserObject
{
    public int id = 0;
    public string firstName = "Henderson";
    public string lastName = "Simonis";
    public string email = "Henderson_Simonis@hotmail.com";
    public string gender = "Female";
    public Order[] orders = new Order[]
    {
        new Order { orderId = 0, item = "orange", quantity = 4 },
        new Order { orderId = 1, item = "apple", quantity = 2 },
    };
}

public struct Order
{
    public int orderId;
    public string item;
    public int quantity;
}
