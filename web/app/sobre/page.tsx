"use client";

import Image from "next/image";
import Navbar from "../components/navbar/navbar";
import { Merriweather } from "next/font/google";

const merriweather = Merriweather({
  subsets: ["latin"],
  weight: ["700"],
});

export default function SobrePage() {
  return (
    <>
      <Navbar />

      <main className="min-h-screen flex flex-col items-center bg-neutral-900 text-white">
        <section className="w-full max-w-[1920px] px-[128px] py-16 flex flex-col md:flex-row items-start justify-between gap-12">
          {/* Texto */}
          <div className="md:w-1/2 mt-3">
            <h1 className={`${merriweather.className} text-4xl font-bold mb-3`}>
              Sobre
            </h1>
            <div className="w-8 h-[3px] bg-cyan-500 mb-5"></div>

            <p className="text-gray-300 leading-relaxed mb-4">
              O <span className="font-semibold">SafeZone</span> é
              uma plataforma voltada para a análise e visualização de dados
              relacionados a casos criminais.
            </p>

            <p className="text-gray-300 leading-relaxed mb-4">
              Nosso objetivo é transformar informações coletadas por meio de
              denúncias em dashboards interativos e acessíveis, facilitando a
              compreensão dos cenários de segurança em diferentes regiões.
              Através de formulários de denúncia, reunimos dados que são
              processados e convertidos em gráficos e mapas.
            </p>

            <p className="text-gray-300 leading-relaxed mb-4">
              Dessa forma, promovemos maior transparência, conscientização
              social e fornecemos subsídios para a tomada de decisão de
              autoridades, organizações e cidadãos preocupados com a segurança
              pública.
            </p>

            <p className="text-gray-300 leading-relaxed">
              O SafeZone acredita que a informação é uma ferramenta poderosa
              para gerar mudanças. Ao dar visibilidade às ocorrências
              criminais, buscamos criar um ambiente mais seguro e colaborativo,
              onde cada denúncia conta e pode fazer a diferença.
            </p>
          </div>

          {/* Imagem */}
          <div className="md:w-1/2 flex justify-center items-end">
            <div className="bg-white rounded-[4rem] p-6 md:p-10">
              <Image
                src="/mascote.svg"
                alt="Mascote SafeZone"
                width={300}
                height={300}
                className="object-contain"
              />
            </div>
          </div>
        </section>
      </main>
    </>
  );
}
