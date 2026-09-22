using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class StoryManager : MonoBehaviour
{
    [SerializeField]private PlayableDirector director; 
    //[SerializeField]private List<ChapterData> chapters = new List<ChapterData>();
   // [SerializeField]private ChapterData startchapter; 
    [SerializeField]private CanvasGroup canvas; 

    public List<PlayableDirector> directors = new List<PlayableDirector> ();

    //private ChapterData _currentchapter; 

    private int currenttimeline = 0; 
    private int _currenttimeline; 

/*
    //returnvalue tells if the chapter was there
    public bool SetChapter(string target)
    {
        bool chapterfound = false; 
        foreach(ChapterData chapter in chapters)
        {
            if(chapter.name == target)
            {
                _currentchapter = chapter;
                chapterfound = true; 
                break; 
            }
        }
        return chapterfound;
    }
*/
    public void playNext()
    {
        Debug.Log("Director Play Currnent: " + currenttimeline + " Directors: " + directors.Count);
        if(currenttimeline >= directors.Count) { currenttimeline = 0; };
        Debug.Log("Playing number " + currenttimeline); 
        director = directors[currenttimeline++];
        director.time = 0.0f; 
        director.Stop();
        director.Evaluate();
        director.Play();
        
        
        //director.paused += OnPlayableDirectorStopped;
        canvas.alpha = 0.5f; 
        canvas.interactable = false; 

        StartCoroutine(ReactivateButton((float)(director.duration)));
        
    }

    void OnPlayableDirectorStopped(PlayableDirector aDirector)
    {
        Debug.Log("Stopped on director");
        if (director == aDirector)
        {
            Debug.Log("PlayableDirector named " + aDirector.name + " is now stopped.");

        }

    }

    private IEnumerator ReactivateButton(float waittime)
    {
        yield return new WaitForSeconds(waittime);
        canvas.alpha = 1.0f; 
        canvas.interactable = true; 
    }





}
