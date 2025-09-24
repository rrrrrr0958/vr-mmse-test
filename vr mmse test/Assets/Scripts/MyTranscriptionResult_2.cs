using System;

[Serializable]
public class MyTranscriptionResult
{
    public string filename;
    public string spoken_text;
    public float accuracy;
    public bool passed;
}
