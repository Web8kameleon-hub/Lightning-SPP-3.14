FROM eclipse-temurin:25-jdk AS build

WORKDIR /src
COPY src/main/java ./src/main/java
RUN mkdir -p /out \
    && find src/main/java -name '*.java' -print0 \
        | xargs -0 javac --release 25 -d /out \
    && jar --create --file /longo.jar --main-class com.clisonix.longo.LongoServer -C /out .

FROM eclipse-temurin:25-jre

RUN useradd --system --uid 10001 --create-home longo
WORKDIR /app
COPY --from=build /longo.jar /app/longo.jar
RUN mkdir -p /data && chown longo:longo /data

USER longo
EXPOSE 8030
ENV LONGO_PORT=8030
ENV LONGO_STORAGE_PATH=/data/longo.bin

CMD ["java", "--add-modules", "jdk.httpserver", "-jar", "/app/longo.jar"]

