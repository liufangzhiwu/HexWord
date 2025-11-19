using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaEvents : MonoBehaviour
{
    public bool feverMode = false;

    public ParticleSystem[] steamP;

    public PhysicsMaterial2D solidLavaMaterial;

    private void Start()
    {
        for (int i = 1; i < steamP.Length; i++)
        {
            steamP[i] = Instantiate(steamP[0]);
        }
    }
    public void OnCollideLava( GameObject p1, GameObject p2)
    {
        /* p1 = particle 1 in the collision*/
        /* p2= particle 2 in the collision */


        if (p1.tag == p2.tag) // don't trigger if collider itself (lava <=> lava)
        {

            return;

            /*
            MetaballParticleClass m1 = p1.GetComponent<MetaballParticleClass>();
            MetaballParticleClass m2 = p2.GetComponent<MetaballParticleClass>();

            if (m1.GetFreeze())
            {
                m2.SetFreeze();
                m2.SetColor(Color.black * .6f);
                m2.GetComponent<Rigidbody2D>().sharedMaterial = solidLavaMaterial;
            }
            
            if (m2.GetFreeze())
            {
                m1.SetFreeze();
                m1.SetColor(Color.black * .6f);
                m2.GetComponent<Rigidbody2D>().sharedMaterial = solidLavaMaterial;
            }
            */


        }



        if (p1.tag == "Metaball_liquid" && p2.tag == "Player") // Water & lava collision
        {
            //Lava 
            MetaballParticleClass m = p2.GetComponent<MetaballParticleClass>();
            if (feverMode)
            {
                m.LifeTime = .2f;
            }
            else
            {
               
                //m.LifeTime = 10f;
            }

            m.SetColor(Color.gray * .6f);
            m.SetHighDensity();
            m.removeGlow();
           // m.ScaleDown = true;


            //Water
            // if (!m.GetFreeze())
            // {
            //m = p1.GetComponent<MetaballParticleClass>();
               // m.LifeTime = 1f;
           //}
           
            // Play Steam particles to simulate water vaporation
            for (int i = 0; i < steamP.Length; i++)
            {
                if (steamP[i].isPlaying)
                    continue;

                steamP[i].transform.position = p1.transform.position;
                steamP[i].Play();
                break;
            }


        }





    }
}
