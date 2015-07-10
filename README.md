# XSerializer
A customizable XML serializer based on XML to LINQ.

## Background
I just want to implement the functionality of `System.Xml.Serialization.XmlSerializer` on my own. Maybe you've noticed that `XmlSerializer` enables us to create & load XML documents from & to classes, and the behaviors can be adjusted via different Attributes. Compared with DataContractSeriallizer, it can give you more control on what the XML document should look like. However, the mechanism of `XmlSerializer` is still rigid in the way that you can't gain full control on some elements / attributes, while maintain the rest of the members clean & tidy. For example, a commonly occurred problem to who use `XmlSerializer` is that, it cannot properly handle multiple references towards the same instance. Instead, the same instance is stored multiple times in XML. Hence, it also cannot handle circular references.

I've searched about it for some time, and found that the reference problem can be solved when using DataContractSeriallizer. However, it stores all the members as XML elements, and this behavior cannot be tweaked.

This is not the only thing that afflicts me. In fact, I thought about bypassing this reference problem by using custom callback functions, but unfortunately, for some reason, `XmlSerializer` does not implement IFormatter, maybe for which neither does it conform to the conventions of serialization formatters like OnSerializingAttribute, OnDeserializingAttribute, etc. This means I cannot adjust members during the serialization / deserialization procedure.

Sure, you can use `IXmlSerializable` to gain full control over the serialization, at the cost of implementing all the details, including those members that *can*(and should) be taken care of by the Serializer, all by yourself.

Also `XmlSerializer` cannot provide extra information during the serialization process, which has been achieved in `IFormatter` series, I mean, `StreamingContext`.

So… I'm going to implement a serializer on my own.

## Features
* Serialization
	* Public property/field serialization.
		* Including built-in simple types & its `Nullable` from.
		* Supports property/field that uses another class as type.
		* Supports `IEnumerable`.
		* Supports "AnyElement" & "AnyAttribute".
		* Supports custom XML name & namespace.
	* Attribute-controlled XML element/field generation.
		* Opt-in member generation mode (That is, only attributed members will be serialized.)
	* Multi-referenced instance serialization. (WIP)
	* Serialization callbacks. (Not Implemented Yet)
* Deserialization (Uh oh… Not Implemented Yet)

## Some Details
Now I only expect to implement these features ASAP, so I use XML to LINQ, which simplifies the process of XML.

I also use reflection to access members, which in future, may be switchd to some more efficient ways, like dynamically-generated assemblies or something.
