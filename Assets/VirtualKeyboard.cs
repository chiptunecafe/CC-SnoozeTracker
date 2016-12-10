﻿using UnityEngine;
using System.Collections;

public class VirtualKeyboard : MonoBehaviour {
    public enum Note { None = 0, C, Cs, D, Ds, E, F, Fs, G, Gs, A, As, B, NoteOff }

    [System.Serializable]
    public class NoteKey
    {
        public Note note;
        public KeyCode key;
        public int octaveOffset;

        public bool GetNoteDown(int octave, out byte noteData)
        {
            if (Input.GetKeyDown(key))
            {
                int result = (octave + octaveOffset) << 4;
                result |= (int)note;
                noteData = (byte)result;
                return true;
            }

            noteData = 0;
            return false;
        }

        public bool GetNoteUp()
        {
            return Input.GetKeyUp(key);
        }
    }

    public PSGWrapper psg;
    public SongPlayback playback;
    public PatternView patternView;
    public Instruments instruments;
    public int currentOctave = 3;
    public int currentInstrument;
    public int patternAdd = 1;
    public NoteKey[] noteBinds;

    private Instruments.InstrumentInstance m_Instrument;

    void Awake() {
        psg.AddIrqCallback ( 50, OnIrqCallback );
        psg.AddIrqCallback(Instruments.InstrumentInstance.SAMPLE_RATE, OnSampleCallback);
    }

    void Update()
    {
        int sel = patternView.selection;
        if (sel % SongData.SONG_DATA_COUNT != 0)
            return;

        for (int i = 0; i < noteBinds.Length; i++)
        {
            byte noteData;
            if (noteBinds[i].GetNoteDown(currentOctave, out noteData))
            {
                patternView.data[sel] = noteData;
                patternView.data [ sel + 1 ] = (byte)currentInstrument;
                patternView.MoveLine(patternAdd);

                if (noteBinds[i].note != Note.None && noteBinds[i].note != Note.NoteOff)
                {
                    m_Instrument = instruments.presets [ currentInstrument ];
                    m_Instrument.note = noteBinds[i].note;
                    m_Instrument.octave = currentOctave + noteBinds[i].octaveOffset;
                    m_Instrument.relativeVolume = 0xF;
                }
            }
        }

        for ( int i = 0 ; i < noteBinds.Length ; i++ ) {
            if ( noteBinds [ i ].GetNoteUp ( ) && m_Instrument.note == noteBinds [ i ].note ) {
                m_Instrument.note = Note.NoteOff;
            }
        }
    }

    private void OnIrqCallback() {
        if ( playback.isPlaying )
            return;

        m_Instrument.UpdatePSG ( psg, patternView.selectedChannel );
    }

    private void OnSampleCallback()
    {
        if ( playback.isPlaying )
            return;

        m_Instrument.UpdatePSGSample(psg, patternView.selectedChannel);
    }

    public static Note GetNote(int noteData)
    {
        return (Note)(noteData & 0x0F);
    }

    public static int GetOctave(int noteData)
    {
        return (noteData >> 4) & 0x0F;
    }

	public static string FormatNote(int noteData)
    {
        int note = noteData & 0x0F;
        int octave = (noteData >> 4) & 0x0F;
        string result = ((Note)note).ToString() + octave.ToString();

        if ((Note)note == Note.None)
            result = "--";
        else if ((Note)note == Note.NoteOff)
            result = "OFF";
        else
            result = result.Replace('s', '#');

        return result;
    }
}
