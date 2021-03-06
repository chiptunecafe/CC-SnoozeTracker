﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatternView : MonoBehaviour {
    public struct MatrixPosition {
        public int line;
        public int channel;
        public int dataColumn;
    }

    public int selectedLine {
        get { return m_CurrentPosition.line; }
        set {
            SetSelection ( value );
        }
    }
    public MatrixPosition position { get { return m_CurrentPosition; } }

    public CanvasGroup[] channels;
    public Transform lineNumbers;
    public GameObject lineNumberPrefab;
    public GameObject patternRowPrefab;
    public SongData data;
    public SongPlayback playback;
    public Instruments instruments;
    public ScrollRect scroll;
    public Image selection;
    public float lineHeight;
    public int highlightInterval;
    public Color highlight;
    public Color selectionRecording;
    public Color selectionNormal;
    public BoxSelection boxSelection;


    [HideInInspector]
    public bool recording;

    private MatrixPosition m_CurrentPosition;
    private int m_CurrentLength;
    private string m_Input;
    private int m_InputPos;

    private List<Text> m_LineNumbers = new List<Text>();
    private List<PatternRow>[] m_PatternRows;

	// Use this for initialization
	void Start () {
        SetSelection ( 0, 0, 0, false );
	}
	
	// Update is called once per frame
	void Update () {
	    if (ExclusiveFocus.hasFocus)
	        return;
	    
        if ( Input.GetKeyDown ( KeyCode.Space ) ) {
            selection.color = recording ? selectionRecording : selectionNormal;
            recording = !recording;
        }

        if (recording && !KeyboardShortcuts.ModifierDown()) {
            int maxLen = m_CurrentPosition.dataColumn == 2 ? 1 : 2;

            if (m_CurrentPosition.dataColumn != 0 && Input.inputString.Length > 0) {
                if(selectedLine + m_CurrentPosition.dataColumn != m_InputPos) {
                    m_InputPos = selectedLine + m_CurrentPosition.dataColumn;
                    m_Input = System.String.Empty;
                }
                m_Input += Input.inputString[0];

                int res;
                if(int.TryParse(m_Input, System.Globalization.NumberStyles.HexNumber, null, out res)) {
                    SetDataAtSelection(res);
                } else {
                    m_Input = m_Input.Substring(0, m_Input.Length - 1);
                }

                if (m_Input.Length >= maxLen) {
                    MoveVertical(1);
                    m_Input = System.String.Empty;
                }

            }
        }
    }

    public void UpdatePatternLength() {
        if ( m_CurrentLength == data.patternLength )
            return;
        
        if(m_CurrentLength > 0)
            SetSelection(0, 0, 0, false);
        
        if(m_PatternRows == null ) {
            m_PatternRows = new List<PatternRow> [ channels.Length ];
            for ( int i = 0 ; i < channels.Length ; i++ ) {
                m_PatternRows [ i ] = new List<PatternRow> ( );
            }
        }

        if ( m_CurrentLength < data.patternLength ) {
            for ( int i = 0 ; i < data.patternLength - m_CurrentLength; i++ ) {
                GameObject lineNum = Instantiate ( lineNumberPrefab, lineNumbers );
                m_LineNumbers.Add ( lineNum.GetComponentInChildren<Text> ( ) );

                for ( int p = 0 ; p < channels.Length ; p++ ) {
                    GameObject rowObj = Instantiate ( patternRowPrefab, channels [ p ].transform );
                    PatternRow row = rowObj.GetComponent<PatternRow> ( );
                    row.view = this;
                    row.channel = p;
                    row.UpdateData ( );
                    m_PatternRows[p].Add ( row );
                }
            }
        } else {
            int removeCount = m_CurrentLength - data.patternLength;
            Debug.Log ( "Remove " + removeCount + " entries" );

            for ( int i = 0 ; i < removeCount ; i++ ) {
                Destroy ( m_LineNumbers [ data.patternLength + i ].transform.parent.gameObject );
            }

            m_LineNumbers.RemoveRange ( data.patternLength, removeCount );

            for ( int i = 0 ; i < channels.Length ; i++ ) {
                for ( int p = 0 ; p < removeCount ; p++ ) {
                    Destroy ( m_PatternRows [ i ] [ data.patternLength + p ].gameObject );
                }

                m_PatternRows[i].RemoveRange ( data.patternLength, removeCount );

                UpdatePatternChannel ( i );
            }
        }

        m_CurrentLength = data.patternLength;
        UpdateLineNumbers ( );
    }

    public bool IsCurrentPatternValid() {
        return data.IsPatternValid(position.channel);
    }

    public void UpdatePatternData() {
        for ( int i = 0 ; i < m_PatternRows.Length ; i++ ) {
            UpdatePatternChannel(i);
        }
    }

    public void UpdatePatternChannel(int channel) {
        if ( m_CurrentLength != data.patternLength )
            return;

        for (int i = 0; i < data.patternLength; i++) {
            m_PatternRows[channel][i].UpdateData();
        }

        channels[channel].alpha = data.lookupTable[data.currentPattern][channel] >= 0 ? 1f : 0.5f;
    }
    
    public void UpdateSelection() {
        UpdateSingleRow (m_CurrentPosition.channel, m_CurrentPosition.line);
    }

    public void UpdateSingleRow(int channel, int line) {
        m_PatternRows [ channel ] [ line ].UpdateData ( );
    }

    public void SetDataAtSelection(int data, int colOffset = 0) {
        this.data.SetData (m_CurrentPosition.channel, m_CurrentPosition.line, m_CurrentPosition.dataColumn + colOffset, data );
        UpdateSelection ( );
    }

    public int GetDataAtSelection(int colOffset = 0) {
        return data.GetData(m_CurrentPosition.channel, m_CurrentPosition.line, m_CurrentPosition.dataColumn + colOffset);
    }

    public PatternRow GetRowAt(int channel, int line) {
        return m_PatternRows [ channel ] [ line ];
    }

    public BoxSelectable GetCurrentSelectable() {
        return m_PatternRows [ position.channel ] [ position.line ].GetSelectable ( position.dataColumn );
    }

    private void UpdateLineNumbers() {
        for ( int i = 0 ; i < m_LineNumbers.Count ; i++ ) {
            m_LineNumbers [ i ].text = i.ToString ( "X2" );
        }
    }

    public void MoveVertical(int increment) {
        boxSelection.CheckShiftMovementBegin ( );

        int line = m_CurrentPosition.line + increment;

        if ( line > data.patternLength )
            line = 0;
        else if ( line < 0 )
            line = data.patternLength - 1;

        SetSelection ( line );

        boxSelection.CheckShiftMovementUpdate ( );
    }

    public void MoveHorizontal(int increment) {
        boxSelection.CheckShiftMovementBegin ( );

        int column = m_CurrentPosition.dataColumn + increment;
        int channel = m_CurrentPosition.channel;

        if ( column >= PatternRow.numDataEntries ) {
            column = 0;
            channel++;
        } else if ( column < 0 ) {
            column = PatternRow.numDataEntries - 1;
            channel--;
        }

        if ( channel >= channels.Length ) {
            channel = 0;
        } else if ( channel < 0 ) {
            channel = channels.Length - 1;
        }

        SetSelection (m_CurrentPosition.line, channel, column, false );
        boxSelection.CheckShiftMovementUpdate ( );
    }

    public void SetSelection(MatrixPosition position) {
        SetSelection(position.line, position.channel, position.dataColumn, false);
    }

    public void SetSelection(int line, int channel = -1, int column = -1, bool fitSelection = true) {
        if ( line >= data.patternLength )
            line = 0;

        m_PatternRows[m_CurrentPosition.channel][m_CurrentPosition.line].Deselect();
        int lineDelta = line - m_CurrentPosition.line;
        m_CurrentPosition.line = line;

        if (channel >= 0) {
            m_CurrentPosition.channel = channel;
            if (column >= 0)
                m_CurrentPosition.dataColumn = column;
        }

        Vector2 selPos = selection.rectTransform.anchoredPosition;
        Vector3 scrollPos = scroll.content.localPosition;

        RectTransform parentRect = transform.parent.GetComponent<RectTransform>();

        if (playback.isPlaying) {
            if (scroll.enabled) {
                scroll.enabled = false;
                selection.transform.SetParent(transform.parent);
            }

            float offset = -parentRect.rect.height * 0.5f;
            scrollPos.y = -m_PatternRows[m_CurrentPosition.channel][m_CurrentPosition.line].transform.localPosition.y + offset;
            scroll.verticalScrollbar.value = 1 - ((float)m_CurrentPosition.line / data.patternLength);
            selPos.y = offset + 10;
            
            selection.rectTransform.anchoredPosition = selPos;
        } else {
            selPos.y = -lineHeight * line;
            if (!scroll.enabled) {
                scroll.enabled = true;
                selection.transform.SetParent(transform);
            }
            
            selection.rectTransform.anchoredPosition = selPos;
            if (fitSelection) {
                Vector3 selScroll = scroll.viewport.InverseTransformPoint(selection.transform.position - Vector3.up * 20);
                if (!scroll.viewport.rect.Contains(selScroll)) {
                    if (lineDelta < 0)
                        scrollPos.y = line * selection.rectTransform.rect.height;
                    else
                        scrollPos.y = line * selection.rectTransform.rect.height - parentRect.rect.height +
                                      selection.rectTransform.rect.height;
                }
            }
        }

        scroll.content.localPosition = scrollPos;
        m_PatternRows[m_CurrentPosition.channel ] [m_CurrentPosition.line ].Select (m_CurrentPosition.dataColumn);
    }
}
